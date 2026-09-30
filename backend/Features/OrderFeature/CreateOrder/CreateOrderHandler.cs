using System.Security.Cryptography;
using System.Text.Json;
using backend.Common.Carts;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.OrderFeature.CreateOrder;

/// <summary>
/// Shape-only checks for the one command in the project that an unauthenticated
/// caller can send. Business rules — which fields guest checkout requires, stock,
/// shipping zones, payment method — stay in <see cref="CreateOrderHandler"/>.
///
/// Two of these caps matter more than the rest: <c>customer_notes</c>,
/// <c>shipping_address1</c> and <c>shipping_address2</c> are unbounded `text`
/// columns, so Postgres accepts whatever it is handed and nothing downstream
/// would have stopped an anonymous caller from writing megabytes per order. The
/// remaining limits mirror the varchar lengths in AppDbContext, turning what
/// would be a 500 from the driver into a 400 that names the field.
///
/// Messages are Arabic because this is the only command whose failures reach a
/// shopper rather than an admin.
/// </summary>
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    // Unbounded text columns — these numbers are the only limit that exists.
    // customerNotes matches the storefront's own zod cap so the two agree.
    private const int CustomerNotesMax = 500;
    private const int AddressLineMax = 255;

    // Mirror the varchar lengths declared on orders in AppDbContext.
    private const int NameMax = 100;
    private const int CityMax = 100;
    private const int GovernorateMax = 100;
    private const int CountryMax = 100;
    private const int PhoneMax = 20;

    public CreateOrderValidator()
    {
        RuleFor(x => x.ShippingFirstName)
            .MaximumLength(NameMax)
            .WithMessage($"الاسم الأول يجب ألا يتجاوز {NameMax} حرفاً");

        RuleFor(x => x.ShippingLastName)
            .MaximumLength(NameMax)
            .WithMessage($"الاسم الأخير يجب ألا يتجاوز {NameMax} حرفاً");

        // Digits, spaces and the punctuation people actually type in a phone
        // number. Deliberately permissive about format — this is a length and
        // character-class guard, not an Iraqi numbering-plan check.
        RuleFor(x => x.ShippingPhone)
            .MaximumLength(PhoneMax)
            .WithMessage($"رقم الهاتف يجب ألا يتجاوز {PhoneMax} رقماً")
            .Matches(@"^[0-9+\-\s()]+$")
            .WithMessage("رقم الهاتف يحتوي على محارف غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.ShippingPhone));

        RuleFor(x => x.ShippingAddress1)
            .MaximumLength(AddressLineMax)
            .WithMessage($"العنوان يجب ألا يتجاوز {AddressLineMax} حرفاً");

        RuleFor(x => x.ShippingAddress2)
            .MaximumLength(AddressLineMax)
            .WithMessage($"تتمة العنوان يجب ألا تتجاوز {AddressLineMax} حرفاً");

        RuleFor(x => x.ShippingCity)
            .MaximumLength(CityMax)
            .WithMessage($"المدينة يجب ألا تتجاوز {CityMax} حرفاً");

        RuleFor(x => x.ShippingGovernorate)
            .MaximumLength(GovernorateMax)
            .WithMessage($"المحافظة يجب ألا تتجاوز {GovernorateMax} حرفاً");

        RuleFor(x => x.ShippingCountry)
            .MaximumLength(CountryMax)
            .WithMessage($"الدولة يجب ألا تتجاوز {CountryMax} حرفاً");

        // M-6: there is no coupon feature — no coupons table and no discount
        // logic — yet the code used to be accepted and stored with a zero
        // discount, which reads to a shopper (and to staff) like a promise that
        // was not kept. Refused outright until coupons actually exist.
        RuleFor(x => x.CouponCode)
            .Must(string.IsNullOrWhiteSpace)
            .WithMessage("الكوبونات غير متاحة حالياً");

        RuleFor(x => x.CustomerNotes)
            .MaximumLength(CustomerNotesMax)
            .WithMessage($"الملاحظات يجب ألا تتجاوز {CustomerNotesMax} حرفاً");

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50)
            .WithMessage("طريقة الدفع غير صالحة");
    }
}

public class CreateOrderHandler(AppDbContext context)
    : IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>
{
    // Cash on delivery is the only method with anything behind it: there is no
    // gateway integration and no bank-transfer reconciliation, so accepting the
    // others produced orders nobody could collect. The storefront sends "cod"
    // only; widen this when a real method is implemented.
    private static readonly HashSet<string> ValidPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cod"
    };

    // No 0/O, 1/I/L: the code is read aloud over the phone and typed by hand.
    private const string OrderCodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int OrderCodeLength = 8;

    private const string PricesChangedMessage =
        "تغيّرت أسعار بعض المنتجات في سلتك. راجع السلة ثم أكّد الطلب من جديد.";

    public async Task<Result<CreateOrderResponse>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.SessionId))
            return Result<CreateOrderResponse>.Failure("Cart session is required");

        if (!CartSessionId.TryNormalize(command.SessionId, out var sessionId))
            return Result<CreateOrderResponse>.Failure(CartSessionId.InvalidMessage);

        var paymentMethod = command.PaymentMethod.Trim().ToLowerInvariant();
        if (!ValidPaymentMethods.Contains(paymentMethod))
            return Result<CreateOrderResponse>.Failure("Invalid payment method");

        if (command.UserId is not null)
        {
            var userExists = await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken);
            if (!userExists)
                return Result<CreateOrderResponse>.Failure("User not found");
        }

        var shipping = await ResolveShippingAsync(command, cancellationToken);
        if (shipping.Error is not null)
            return Result<CreateOrderResponse>.Failure(shipping.Error);

        var cart = await context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Variant)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId, cancellationToken);

        if (cart is null || cart.CartItems.Count == 0)
            return Result<CreateOrderResponse>.Failure("Cart is empty");

        foreach (var item in cart.CartItems)
        {
            if (!string.Equals(item.Product.Status, "active", StringComparison.OrdinalIgnoreCase))
                return Result<CreateOrderResponse>.Failure($"Product '{item.Product.NameEn}' is not available");

            if (item.VariantId is not null && item.Variant is not null && !item.Variant.IsActive)
                return Result<CreateOrderResponse>.Failure($"Product variant for '{item.Product.NameEn}' is not available");

            var availableStock = item.Variant?.StockQuantity ?? item.Product.StockQuantity;
            if (item.Quantity > availableStock)
                return Result<CreateOrderResponse>.Failure($"Insufficient stock for '{item.Product.NameEn}'");
        }

        // M-10: the cart keeps the price each item had when it was added, and
        // that is what the shopper has been looking at. An order must never be
        // placed at a price that no longer exists, in either direction — so a
        // change since then refreshes the cart and asks the shopper to confirm
        // the new total instead of silently charging (or losing) the difference.
        var now = DateTime.Now;
        var pricesChanged = false;
        foreach (var item in cart.CartItems)
        {
            var currentPrice = ResolveUnitPrice(item.Product, item.Variant);
            if (item.UnitPrice == currentPrice)
                continue;

            // A null price predates price capture; the cart already showed the
            // current price for it, so there is nothing to re-confirm.
            if (item.UnitPrice is not null)
                pricesChanged = true;

            item.UnitPrice = currentPrice;
            item.UpdatedAt = now;
        }

        if (pricesChanged)
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result<CreateOrderResponse>.Failure(PricesChangedMessage);
        }

        var subtotal = cart.CartItems.Sum(ci => ci.UnitPrice!.Value * ci.Quantity);

        var (shippingAmount, zoneId, shippingError) = await CalculateShippingAsync(
            subtotal,
            shipping.ShippingZoneId,
            shipping.ShippingGovernorate,
            cancellationToken);

        if (shippingError is not null)
            return Result<CreateOrderResponse>.Failure(shippingError);

        var discountAmount = 0m;
        var taxAmount = 0m;
        var total = subtotal - discountAmount + shippingAmount + taxAmount;
        var currency = cart.CartItems.First().Product.Currency;
        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

        var order = new Order
        {
            UserId = command.UserId,
            OrderNumber = orderNumber,
            Status = "pending",
            PaymentStatus = "pending",
            FulfillmentStatus = "unfulfilled",
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingAmount = shippingAmount,
            TaxAmount = taxAmount,
            Total = total,
            Currency = currency,
            ShippingFirstName = shipping.ShippingFirstName,
            ShippingLastName = shipping.ShippingLastName,
            ShippingPhone = shipping.ShippingPhone,
            ShippingAddress1 = shipping.ShippingAddress1,
            ShippingAddress2 = shipping.ShippingAddress2,
            ShippingCity = shipping.ShippingCity,
            ShippingGovernorate = shipping.ShippingGovernorate,
            ShippingCountry = shipping.ShippingCountry,
            ShippingZoneId = zoneId ?? shipping.ShippingZoneId,
            PaymentMethod = paymentMethod,
            CustomerNotes = command.CustomerNotes?.Trim(),
            IpAddress = command.IpAddress?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        // H-4: the stock check above is only a fast, friendly failure. It read
        // stock in one statement and the old code wrote it back in another, so
        // two checkouts for the last unit both passed and sold it twice. The real
        // guard is now a conditional decrement — `stock >= quantity` evaluated
        // by Postgres on the row it updates — inside one transaction with the
        // order insert. A concurrent checkout blocks on the row lock, re-checks
        // against the committed stock, and fails cleanly if it ran out.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var cartItem in cart.CartItems)
        {
            var unitPrice = cartItem.UnitPrice!.Value;
            var lineTotal = unitPrice * cartItem.Quantity;

            order.OrderItems.Add(new OrderItem
            {
                ProductId = cartItem.ProductId,
                VariantId = cartItem.VariantId,
                ProductName = cartItem.Product.NameEn,
                Sku = cartItem.Variant?.Sku ?? cartItem.Product.Sku,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                CompareAtPrice = cartItem.Variant?.CompareAtPrice ?? cartItem.Product.CompareAtPrice,
                DiscountAmount = 0,
                TotalPrice = lineTotal,
                WarrantyMonths = cartItem.Product.WarrantyMonths,
                RequiresShipping = cartItem.Product.RequiresShipping,
                CreatedAt = now
            });

            var quantity = cartItem.Quantity;
            var reserved = cartItem.VariantId is not null
                ? await context.ProductVariants
                    .Where(v => v.Id == cartItem.VariantId && v.StockQuantity >= quantity)
                    .ExecuteUpdateAsync(
                        set => set.SetProperty(v => v.StockQuantity, v => v.StockQuantity - quantity),
                        cancellationToken)
                : await context.Products
                    .Where(p => p.Id == cartItem.ProductId && p.StockQuantity >= quantity)
                    .ExecuteUpdateAsync(
                        set => set.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity),
                        cancellationToken);

            // Returning disposes the transaction, which rolls back any unit
            // already reserved for an earlier line of this order.
            if (reserved == 0)
                return Result<CreateOrderResponse>.Failure($"Insufficient stock for '{cartItem.Product.NameEn}'");

            // Incremented in SQL as well: a read-modify-write here would lose
            // counts under the same concurrency the stock guard exists for.
            await context.Products
                .Where(p => p.Id == cartItem.ProductId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(p => p.TotalSold, p => p.TotalSold + quantity)
                        .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken);
        }

        order.OrderStatusHistories.Add(new OrderStatusHistory
        {
            FromStatus = null,
            ToStatus = "pending",
            ChangedByType = "customer",
            Note = "Order placed",
            CreatedAt = now
        });

        context.Orders.Add(order);
        context.CartItems.RemoveRange(cart.CartItems);
        cart.UpdatedAt = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two submissions of the same cart (a double click, a retried
            // request): the other one committed first and already removed these
            // items. This one rolls back, stock included, instead of creating a
            // second order for goods that were paid for once.
            return Result<CreateOrderResponse>.Failure("Cart has already been checked out");
        }

        await transaction.CommitAsync(cancellationToken);

        var items = order.OrderItems.Select(oi => new CreateOrderItemResponse(
            oi.Id,
            oi.ProductId,
            oi.VariantId,
            oi.ProductName,
            oi.Sku,
            oi.Quantity,
            oi.UnitPrice,
            oi.TotalPrice)).ToList();

        var response = new CreateOrderResponse(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.Status,
            order.PaymentStatus,
            order.FulfillmentStatus,
            order.Subtotal,
            order.DiscountAmount,
            order.ShippingAmount,
            order.TaxAmount,
            order.Total,
            order.Currency,
            order.ShippingFirstName,
            order.ShippingLastName,
            order.ShippingPhone,
            order.ShippingAddress1,
            order.ShippingCity,
            order.ShippingGovernorate,
            order.ShippingCountry,
            order.ShippingZoneId,
            order.PaymentMethod,
            order.CreatedAt,
            items);

        return Result<CreateOrderResponse>.Success(response);
    }

    private static decimal ResolveUnitPrice(Product product, ProductVariant? variant) =>
        variant?.Price ?? product.Price;

    /// <summary>
    /// M-3: the order number is half of the guest-tracking credential (with the
    /// phone), so it must not be guessable. It used to be six decimal digits from
    /// Random.Shared — 900,000 values a day from a generator whose state can be
    /// inferred from observed outputs. Eight characters from a 31-symbol alphabet
    /// drawn with a cryptographic RNG give ~8.5e11 values per day, which together
    /// with the track endpoint's own rate limit puts enumeration out of reach.
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = new string(RandomNumberGenerator.GetItems<char>(OrderCodeAlphabet, OrderCodeLength));
            var orderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{code}";
            var exists = await context.Orders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
            if (!exists)
                return orderNumber;
        }

        // Five collisions in a space this size means something is broken, not unlucky.
        throw new InvalidOperationException("Could not generate a unique order number.");
    }

    private async Task<(decimal Amount, long? ZoneId, string? Error)> CalculateShippingAsync(
        decimal subtotal,
        long? shippingZoneId,
        string? governorate,
        CancellationToken cancellationToken)
    {
        ShippingZone? zone = null;

        if (shippingZoneId is not null)
        {
            zone = await context.ShippingZones
                .Include(z => z.ShippingRates)
                .FirstOrDefaultAsync(z => z.Id == shippingZoneId && z.IsActive, cancellationToken);

            if (zone is null)
                return (0, null, "Shipping zone not found");
        }
        else if (!string.IsNullOrWhiteSpace(governorate))
        {
            var normalizedGovernorate = governorate.Trim();
            var zones = await context.ShippingZones
                .Include(z => z.ShippingRates)
                .Where(z => z.IsActive)
                .ToListAsync(cancellationToken);

            zone = zones.FirstOrDefault(z => ZoneCoversGovernorate(z.Governorates, normalizedGovernorate));

            if (zone is null)
                return (0, null, "No shipping zone found for governorate");
        }
        else
        {
            return (0, null, "Shipping zone or governorate is required");
        }

        var rate = zone.ShippingRates
            .Where(r => r.IsActive && string.Equals(r.RateType, "flat", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Price)
            .FirstOrDefault();

        if (rate is null)
            return (0, zone.Id, "No active shipping rate found for zone");

        if (rate.FreeShippingThreshold is not null && subtotal >= rate.FreeShippingThreshold.Value)
            return (0, zone.Id, null);

        if (rate.MinOrderValue is not null && subtotal < rate.MinOrderValue.Value)
            return (0, zone.Id, $"Minimum order value for shipping is {rate.MinOrderValue.Value}");

        if (rate.MaxOrderValue is not null && subtotal > rate.MaxOrderValue.Value)
            return (0, zone.Id, $"Maximum order value for shipping is {rate.MaxOrderValue.Value}");

        return (rate.Price, zone.Id, null);
    }

    private static bool ZoneCoversGovernorate(string? governoratesJson, string governorate)
    {
        if (string.IsNullOrWhiteSpace(governoratesJson))
            return false;

        try
        {
            var governorates = JsonSerializer.Deserialize<List<string>>(governoratesJson);
            return governorates?.Any(g =>
                string.Equals(g.Trim(), governorate, StringComparison.OrdinalIgnoreCase)) ?? false;
        }
        catch (JsonException)
        {
            return governoratesJson.Contains(governorate, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<(string? Error, string? ShippingFirstName, string? ShippingLastName, string? ShippingPhone,
        string? ShippingAddress1, string? ShippingAddress2, string? ShippingCity, string? ShippingGovernorate,
        string ShippingCountry, long? ShippingZoneId)> ResolveShippingAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AddressId is not null)
        {
            if (command.UserId is null)
                return ("AddressId requires a registered user", null, null, null, null, null, null, null, "Iraq", null);

            var address = await context.Addresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == command.AddressId && a.UserId == command.UserId, cancellationToken);

            if (address is null)
                return ("Address not found", null, null, null, null, null, null, null, "Iraq", null);

            return (null,
                address.FirstName,
                address.LastName,
                address.Phone,
                address.Address1,
                address.Address2,
                address.City,
                address.Governorate,
                address.Country,
                command.ShippingZoneId);
        }

        if (command.UserId is null)
        {
            if (string.IsNullOrWhiteSpace(command.ShippingFirstName))
                return ("Shipping first name is required for guest checkout", null, null, null, null, null, null, null, "Iraq", null);

            if (string.IsNullOrWhiteSpace(command.ShippingPhone))
                return ("Shipping phone is required for guest checkout", null, null, null, null, null, null, null, "Iraq", null);

            if (string.IsNullOrWhiteSpace(command.ShippingAddress1))
                return ("Shipping address is required for guest checkout", null, null, null, null, null, null, null, "Iraq", null);

            if (string.IsNullOrWhiteSpace(command.ShippingCity))
                return ("Shipping city is required for guest checkout", null, null, null, null, null, null, null, "Iraq", null);

            if (string.IsNullOrWhiteSpace(command.ShippingGovernorate))
                return ("Shipping governorate is required for guest checkout", null, null, null, null, null, null, null, "Iraq", null);
        }
        else
        {
            var hasInlineShipping = !string.IsNullOrWhiteSpace(command.ShippingAddress1)
                                    && !string.IsNullOrWhiteSpace(command.ShippingPhone);

            if (!hasInlineShipping)
                return ("Shipping information or addressId is required", null, null, null, null, null, null, null, "Iraq", null);
        }

        var country = string.IsNullOrWhiteSpace(command.ShippingCountry) ? "Iraq" : command.ShippingCountry.Trim();

        return (null,
            command.ShippingFirstName?.Trim(),
            command.ShippingLastName?.Trim(),
            command.ShippingPhone?.Trim(),
            command.ShippingAddress1?.Trim(),
            command.ShippingAddress2?.Trim(),
            command.ShippingCity?.Trim(),
            command.ShippingGovernorate?.Trim(),
            country,
            command.ShippingZoneId);
    }
}
