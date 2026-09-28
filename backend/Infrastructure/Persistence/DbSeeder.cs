using backend.Domain.Entities;
using backend.Features.ShippingZoneFeature;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AttributeEntity = backend.Domain.Entities.Attribute;

namespace backend.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string SeedAdminPassword = "Admin123!";
    private static readonly DateTime Now = DateTime.Now;

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        var refs = new SeedReferences();

        await SeedAdminUsersAsync(db, refs, logger);
        await SeedBrandsAsync(db, refs, logger);
        await SeedCategoriesAsync(db, refs, env, logger);
        await SeedAttributeGroupsAsync(db, refs, logger);
        await SeedAttributesAsync(db, refs, logger);
        await SeedTagsAsync(db, refs, logger);
        await SeedShippingZonesAsync(db, refs, logger);
        await SeedPaymentMethodsAsync(db, refs, logger);
        await SeedSuppliersAsync(db, refs, logger);
        await SeedUsersAsync(db, refs, logger);
        await SeedPagesAsync(db, refs, logger);
        await SeedSettingsAsync(db, refs, logger);

        await SeedShippingRatesAsync(db, refs, logger);
        await SeedProductsAsync(db, refs, logger);
        await SeedDeliveryAgentsAsync(db, refs, logger);
        await SeedDiscountsAsync(db, refs, logger);

        await SeedProductImagesAsync(db, refs, env, logger);
        await SeedProductVariantsAsync(db, refs, logger);
        await SeedProductAttributeValuesAsync(db, refs, logger);
        await SeedProductTagsAsync(db, refs, logger);
        await SeedCollectionsAsync(db, refs, env, logger);
        await SeedBannersAsync(db, refs, logger);
        await SeedMenusAsync(db, refs, logger);
        await SeedAddressesAsync(db, refs, logger);
        await SeedCartsAsync(db, refs, logger);

        await SeedCollectionProductsAsync(db, refs, logger);
        await SeedCartItemsAsync(db, refs, logger);
        await SeedOrdersAsync(db, refs, logger);
        await SeedFlashSalesAsync(db, refs, logger);
        await SeedPurchaseOrdersAsync(db, refs, logger);
        await SeedMenuItemsAsync(db, refs, logger);

        await SeedOrderItemsAsync(db, refs, logger);
        await SeedOrderStatusHistoryAsync(db, refs, logger);
        await SeedFlashSaleProductsAsync(db, refs, logger);
        await SeedPurchaseOrderItemsAsync(db, refs, logger);
        await SeedPaymentTransactionsAsync(db, refs, logger);
        await SeedOrderDeliveriesAsync(db, refs, logger);
        await SeedWishlistsAsync(db, refs, logger);
        await SeedBackInStockRequestsAsync(db, refs, logger);
        await SeedInventoryMovementsAsync(db, refs, logger);

        await SeedReviewsAsync(db, refs, logger);
        await SeedReturnsAsync(db, refs, logger);
        await SeedDiscountUsagesAsync(db, refs, logger);
        await SeedCashCollectionsAsync(db, refs, logger);

        await SeedReturnItemsAsync(db, refs, logger);
        await SeedWarrantyClaimsAsync(db, refs, logger);
        await SeedNotificationsAsync(db, refs, logger);
        await SeedActivityLogsAsync(db, refs, logger);

        await MigrateExternalImageUrlsAsync(db, env, logger);

        logger.LogInformation("Database seeding completed.");
    }

    private static async Task MigrateExternalImageUrlsAsync(
        AppDbContext db,
        IWebHostEnvironment env,
        ILogger logger)
    {
        var migrated = 0;
        var skipped = 0;

        var productImages = await db.ProductImages
            .Where(pi => pi.ImageUrl.StartsWith("http"))
            .ToListAsync();

        foreach (var image in productImages)
        {
            var localUrl = await SeedImageDownloader.TryMigrateExternalUrlAsync(
                env, image.ImageUrl, logger);
            if (localUrl is null)
            {
                skipped++;
                continue;
            }

            image.ImageUrl = localUrl;
            migrated++;
        }

        var categories = await db.Categories
            .Where(c => c.ImageUrl != null && c.ImageUrl.StartsWith("http"))
            .ToListAsync();

        foreach (var category in categories)
        {
            var localUrl = await SeedImageDownloader.TryMigrateExternalUrlAsync(
                env, category.ImageUrl!, logger);
            if (localUrl is null)
            {
                skipped++;
                continue;
            }

            category.ImageUrl = localUrl;
            migrated++;
        }

        var collections = await db.Collections
            .Where(c => c.ImageUrl != null && c.ImageUrl.StartsWith("http"))
            .ToListAsync();

        foreach (var collection in collections)
        {
            var localUrl = await SeedImageDownloader.TryMigrateExternalUrlAsync(
                env, collection.ImageUrl!, logger);
            if (localUrl is null)
            {
                skipped++;
                continue;
            }

            collection.ImageUrl = localUrl;
            migrated++;
        }

        if (migrated > 0)
            await db.SaveChangesAsync();

        if (migrated > 0 || skipped > 0)
        {
            logger.LogInformation(
                "Image migration finished: {Migrated} migrated, {Skipped} skipped",
                migrated,
                skipped);
        }
    }

    private sealed class SeedReferences
    {
        public Dictionary<string, long> AdminUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Brands { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Categories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> AttributeGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> ShippingZones { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> PaymentMethods { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Suppliers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Products { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> ProductVariants { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> DeliveryAgents { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Discounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Orders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> OrderItems { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Addresses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Carts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Menus { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Pages { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> FlashSales { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> PurchaseOrders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> Returns { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Order> OrderEntities { get; } = [];
        public List<Product> ProductEntities { get; } = [];
        public List<User> UserEntities { get; } = [];
    }

    private static string Unsplash(string photoId) =>
        $"https://images.unsplash.com/photo-{photoId}?w=800&q=80";

    private static async Task LoadRefsAsync<T>(
        AppDbContext db,
        DbSet<T> set,
        Dictionary<string, long> dict,
        Func<T, string> keySelector,
        Func<T, long> idSelector) where T : class
    {
        if (dict.Count > 0) return;
        var items = await set.AsNoTracking().ToListAsync();
        foreach (var item in items)
            dict[keySelector(item)] = idSelector(item);
    }

    // ── Tier 1 ──────────────────────────────────────────────────────────────

    private static async Task SeedAdminUsersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync())
        {
            await LoadRefsAsync(db, db.AdminUsers, refs.AdminUsers, u => u.Email, u => u.Id);
            await RepairInvalidSeedAdminPasswordHashesAsync(db, logger);
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedAdminPassword);
        // MustChangePassword stays false here on purpose. These three exist so a
        // developer can sign straight in with a password that is written down in
        // this file; the forced-change flow belongs to accounts whose password is
        // a secret, which is AdminBootstrapper's job and the panel's.
        var users = new[]
        {
            new AdminUser { Name = "Ahmed Al-Subul", Email = "admin@subul.iq", PasswordHash = passwordHash, Role = "superadmin", IsActive = true, PasswordChangedAt = Now, CreatedAt = Now },
            new AdminUser { Name = "Sara Hassan", Email = "sara@subul.iq", PasswordHash = passwordHash, Role = "manager", IsActive = true, PasswordChangedAt = Now, CreatedAt = Now },
            new AdminUser { Name = "Ali Mohammed", Email = "ali@subul.iq", PasswordHash = passwordHash, Role = "staff", IsActive = true, PasswordChangedAt = Now, CreatedAt = Now },
        };
        db.AdminUsers.AddRange(users);
        await db.SaveChangesAsync();
        foreach (var u in users) refs.AdminUsers[u.Email] = u.Id;
        logger.LogInformation("Seeded {Count} admin users", users.Length);
    }

    /// <summary>
    /// Re-hash seed admin passwords when password_hash is missing or not valid bcrypt
    /// (e.g. rows created before the seeder wrote real hashes).
    /// </summary>
    private static async Task RepairInvalidSeedAdminPasswordHashesAsync(AppDbContext db, ILogger logger)
    {
        string[] seedEmails = ["admin@subul.iq", "sara@subul.iq", "ali@subul.iq"];
        var users = await db.AdminUsers
            .Where(u => seedEmails.Contains(u.Email.ToLower()))
            .ToListAsync();

        if (users.Count == 0)
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedAdminPassword);
        var repaired = 0;

        foreach (var user in users)
        {
            if (!NeedsBcryptHashRepair(user.PasswordHash))
                continue;

            user.PasswordHash = passwordHash;
            user.PasswordChangedAt = Now;
            user.MustChangePassword = false;
            user.UpdatedAt = Now;
            repaired++;
        }

        if (repaired == 0)
            return;

        await db.SaveChangesAsync();
        logger.LogInformation("Repaired invalid password hashes for {Count} seed admin users", repaired);
    }

    private static bool NeedsBcryptHashRepair(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return true;

        if (!(hash.StartsWith("$2a$", StringComparison.Ordinal)
              || hash.StartsWith("$2b$", StringComparison.Ordinal)
              || hash.StartsWith("$2y$", StringComparison.Ordinal)))
            return true;

        try
        {
            // Valid bcrypt hashes return false/true; corrupt salts throw.
            _ = BCrypt.Net.BCrypt.Verify("probe", hash);
            return false;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return true;
        }
    }

    private static async Task SeedBrandsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Brands.AnyAsync())
        {
            await LoadRefsAsync(db, db.Brands, refs.Brands, b => b.Slug, b => b.Id);
            return;
        }

        var brands = new (string Name, string Slug, string Logo, bool Featured)[]
        {
            ("ASUS", "asus", "https://upload.wikimedia.org/wikipedia/commons/2/2e/ASUS_Logo.svg", true),
            ("Dell", "dell", "https://upload.wikimedia.org/wikipedia/commons/4/48/Dell_Logo.svg", true),
            ("Lenovo", "lenovo", "https://upload.wikimedia.org/wikipedia/commons/b/b8/Lenovo_logo_2015.svg", true),
            ("HP", "hp", "https://upload.wikimedia.org/wikipedia/commons/a/ad/HP_logo_2012.svg", true),
            ("MSI", "msi", "https://upload.wikimedia.org/wikipedia/commons/0/0a/MSI_Logo.svg", true),
            ("Acer", "acer", "https://upload.wikimedia.org/wikipedia/commons/0/00/Acer_2011.svg", false),
            ("Samsung", "samsung", "https://upload.wikimedia.org/wikipedia/commons/2/24/Samsung_Logo.svg", false),
            ("Logitech", "logitech", "https://upload.wikimedia.org/wikipedia/commons/6/69/Logitech_logo.svg", false),
            ("Kingston", "kingston", "https://upload.wikimedia.org/wikipedia/commons/2/2c/Kingston_Technology_logo.svg", false),
            ("Seagate", "seagate", "https://upload.wikimedia.org/wikipedia/commons/6/62/Seagate_Technology_logo.svg", false),
        };

        foreach (var (name, slug, logo, featured) in brands)
        {
            var brand = new Brand
            {
                Name = name,
                Slug = slug,
                LogoUrl = logo,
                DescriptionEn = $"{name} — trusted electronics brand available at Subul.",
                DescriptionAr = $"{name} — علامة إلكترونيات موثوقة متوفرة في سبل.",
                IsFeatured = featured,
                IsActive = true,
                SortOrder = refs.Brands.Count,
                CreatedAt = Now,
            };
            db.Brands.Add(brand);
            await db.SaveChangesAsync();
            refs.Brands[slug] = brand.Id;
        }
        logger.LogInformation("Seeded {Count} brands", brands.Length);
    }

    private static async Task SeedCategoriesAsync(
        AppDbContext db,
        SeedReferences refs,
        IWebHostEnvironment env,
        ILogger logger)
    {
        if (await db.Categories.AnyAsync())
        {
            await LoadRefsAsync(db, db.Categories, refs.Categories, c => c.Slug, c => c.Id);
            return;
        }

        var parents = new (string En, string Ar, string Slug, string Image)[]
        {
            ("Laptops", "لابتوبات", "laptops", "1587202372775-e229f172b9d7"),
            ("Monitors", "شاشات", "monitors", "1527443224154-c4a3942d3acf"),
            ("Peripherals", "ملحقات", "peripherals", "1541140532154-b024d705b90a"),
            ("Storage", "التخزين", "storage", "1597872200969-2b65d56bd16b"),
            ("PC Components", "مكونات الحاسوب", "pc-components", "1593640408182-31c70c8268f5"),
            ("Networking", "الشبكات", "networking", "1544197150-b99a580bb7a8"),
            ("Printers & Scanners", "الطابعات", "printers-scanners", "1541140532154-b024d705b90a"),
            ("Power & UPS", "الطاقة", "power-ups", "1558618666-fcd25c85cd64"),
            ("Gaming Gear", "معدات الألعاب", "gaming-gear", "1593305841991-05c297ba4575"),
            ("Audio", "الصوت", "audio", "1505740420928-5e560c06d30e"),
        };

        foreach (var (en, ar, slug, img) in parents)
        {
            var cat = new Category
            {
                NameEn = en,
                NameAr = ar,
                Slug = slug,
                ImageUrl = await SeedImageDownloader.ResolveSeedPhotoAsync(env, img),
                DescriptionEn = $"Shop {en} at Subul Iraq electronics store.",
                DescriptionAr = $"تسوق {ar} من متجر سبل للإلكترونيات.",
                SortOrder = refs.Categories.Count,
                IsActive = true,
                CreatedAt = Now,
            };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            refs.Categories[slug] = cat.Id;
        }

        var children = new (string En, string Ar, string Slug, string Parent, string Image)[]
        {
            ("Gaming Laptops", "لابتوبات ألعاب", "gaming-laptops", "laptops", "1587202372775-e229f172b9d7"),
            ("Business Laptops", "لابتوبات أعمال", "business-laptops", "laptops", "1496181133206-80ce9b88a853"),
            ("Gaming Monitors", "شاشات ألعاب", "gaming-monitors", "monitors", "1527443224154-c4a3942d3acf"),
            ("Business Monitors", "شاشات أعمال", "business-monitors", "monitors", "1593640408182-31c70c8268f5"),
            ("Keyboards", "لوحات مفاتيح", "keyboards", "peripherals", "1541140532154-b024d705b90a"),
            ("Mice", "فأرات", "mice", "peripherals", "1527814050087-3793815479db"),
            ("Mouse Pads", "سجادات فأرة", "mouse-pads", "peripherals", "1527814050087-3793815479db"),
            ("Webcams", "كاميرات ويب", "webcams", "peripherals", "1505740420928-5e560c06d30e"),
            ("SSDs", "أقراص SSD", "ssds", "storage", "1597872200969-2b65d56bd16b"),
            ("HDDs", "أقراص HDD", "hdds", "storage", "1597872200969-2b65d56bd16b"),
            ("USB Drives", "فلاش ميموري", "usb-drives", "storage", "1597872200969-2b65d56bd16b"),
            ("GPUs", "بطاقات رسومية", "gpus", "pc-components", "1593640408182-31c70c8268f5"),
            ("RAM", "ذاكرة RAM", "ram", "pc-components", "1593640408182-31c70c8268f5"),
            ("CPU Coolers", "مبردات معالج", "cpu-coolers", "pc-components", "1593640408182-31c70c8268f5"),
            ("Wireless Routers", "راوترات لاسلكية", "wireless-routers", "networking", "1544197150-b99a580bb7a8"),
        };

        foreach (var (en, ar, slug, parent, img) in children)
        {
            var cat = new Category
            {
                ParentId = refs.Categories[parent],
                NameEn = en,
                NameAr = ar,
                Slug = slug,
                ImageUrl = await SeedImageDownloader.ResolveSeedPhotoAsync(env, img),
                SortOrder = refs.Categories.Count,
                IsActive = true,
                CreatedAt = Now,
            };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            refs.Categories[slug] = cat.Id;
        }
        logger.LogInformation("Seeded {Count} categories", parents.Length + children.Length);
    }

    private static async Task SeedAttributeGroupsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.AttributeGroups.AnyAsync())
        {
            await LoadRefsAsync(db, db.AttributeGroups, refs.AttributeGroups, g => g.Slug!, g => g.Id);
            return;
        }

        var groups = new (string En, string Ar, string Slug, bool Filterable)[]
        {
            ("Display Specs", "مواصفات الشاشة", "display-specs", true),
            ("Performance", "الأداء", "performance", true),
            ("Connectivity", "التوصيل", "connectivity", false),
            ("Physical", "المقاسات", "physical", false),
            ("Battery", "البطارية", "battery", false),
        };

        foreach (var (en, ar, slug, filterable) in groups)
        {
            var g = new AttributeGroup
            {
                NameEn = en,
                NameAr = ar,
                Slug = slug,
                SortOrder = refs.AttributeGroups.Count,
                IsFilterable = filterable,
                CreatedAt = Now,
            };
            db.AttributeGroups.Add(g);
            await db.SaveChangesAsync();
            refs.AttributeGroups[slug] = g.Id;
        }
        logger.LogInformation("Seeded {Count} attribute groups", groups.Length);
    }

    private static async Task SeedAttributesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Attributes.AnyAsync())
        {
            await LoadRefsAsync(db, db.Attributes, refs.Attributes, a => a.Slug!, a => a.Id);
            return;
        }

        var attrs = new (string En, string Ar, string Slug, string Group, string InputType, string? Unit, bool Filterable)[]
        {
            ("Screen Size", "حجم الشاشة", "screen-size", "display-specs", "number", "inch", true),
            ("Resolution", "الدقة", "resolution", "display-specs", "text", null, true),
            ("Refresh Rate", "معدل التحديث", "refresh-rate", "display-specs", "number", "Hz", true),
            ("Panel Type", "نوع اللوحة", "panel-type", "display-specs", "text", null, true),
            ("Processor", "المعالج", "processor", "performance", "text", null, true),
            ("RAM", "الذاكرة", "ram", "performance", "number", "GB", true),
            ("Storage", "التخزين", "storage", "performance", "number", "GB", true),
            ("GPU", "بطاقة الرسوميات", "gpu", "performance", "text", null, true),
            ("USB Ports", "منافذ USB", "usb-ports", "connectivity", "number", null, false),
            ("WiFi Standard", "معيار WiFi", "wifi-standard", "connectivity", "text", null, false),
            ("Bluetooth", "بلوتوث", "bluetooth", "connectivity", "boolean", null, false),
            ("Weight", "الوزن", "weight", "physical", "number", "kg", false),
            ("Color", "اللون", "color", "physical", "text", null, true),
            ("Battery Life", "عمر البطارية", "battery-life", "battery", "number", "hours", false),
            ("Capacity", "السعة", "capacity", "battery", "number", "Wh", false),
            ("Print Speed", "سرعة الطباعة", "print-speed", "performance", "number", "ppm", false),
            ("Capacity Storage", "سعة التخزين", "capacity-storage", "performance", "number", "GB", true),
            ("DPI", "DPI", "dpi", "performance", "number", null, false),
            ("Switch Type", "نوع المفاتيح", "switch-type", "physical", "text", null, false),
            ("Connectivity Type", "نوع الاتصال", "connectivity-type", "connectivity", "text", null, false),
        };

        foreach (var (en, ar, slug, group, inputType, unit, filterable) in attrs)
        {
            var a = new AttributeEntity
            {
                GroupId = refs.AttributeGroups[group],
                NameEn = en,
                NameAr = ar,
                Slug = slug,
                Unit = unit,
                InputType = inputType,
                IsFilterable = filterable,
                SortOrder = refs.Attributes.Count,
                CreatedAt = Now,
            };
            db.Attributes.Add(a);
            await db.SaveChangesAsync();
            refs.Attributes[slug] = a.Id;
        }
        logger.LogInformation("Seeded {Count} attributes", attrs.Length);
    }

    private static async Task SeedTagsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Tags.AnyAsync())
        {
            await LoadRefsAsync(db, db.Tags, refs.Tags, t => t.Slug, t => t.Id);
            return;
        }

        var tags = new[] { "gaming", "laptop", "monitor", "keyboard", "mouse", "headset", "ssd", "hdd", "wireless", "rgb", "mechanical", "4k", "ips", "portable", "budget" };
        foreach (var tag in tags)
        {
            var t = new Tag { Name = tag, Slug = tag, CreatedAt = Now };
            db.Tags.Add(t);
            await db.SaveChangesAsync();
            refs.Tags[tag] = t.Id;
        }
        logger.LogInformation("Seeded {Count} tags", tags.Length);
    }

    private static async Task SeedShippingZonesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ShippingZones.AnyAsync())
        {
            // Normalize legacy comma-separated governorates to JSON arrays.
            var legacyZones = await db.ShippingZones
                .Where(z => z.Governorates != null && !z.Governorates.StartsWith("["))
                .ToListAsync();
            if (legacyZones.Count > 0)
            {
                foreach (var zone in legacyZones)
                    zone.Governorates = ShippingZoneGovernorates.Serialize(
                        ShippingZoneGovernorates.Parse(zone.Governorates));
                await db.SaveChangesAsync();
                logger.LogInformation("Normalized governorates JSON on {Count} shipping zones", legacyZones.Count);
            }

            await LoadRefsAsync(db, db.ShippingZones, refs.ShippingZones, z => z.NameEn.ToLowerInvariant().Replace(" ", "-"), z => z.Id);
            return;
        }

        var zones = new (string En, string Ar, string Key, string[] Governorates)[]
        {
            ("Baghdad", "بغداد", "baghdad", ["Baghdad"]),
            ("Basra", "البصرة", "basra", ["Basra"]),
            ("Erbil", "أربيل", "erbil", ["Erbil"]),
            ("Mosul", "الموصل", "mosul", ["Nineveh"]),
            ("Najaf", "النجف", "najaf", ["Najaf"]),
            ("Karbala", "كربلاء", "karbala", ["Karbala"]),
            ("Sulaymaniyah", "السليمانية", "sulaymaniyah", ["Sulaymaniyah"]),
            ("Kirkuk", "كركوك", "kirkuk", ["Kirkuk"]),
            ("Anbar", "الأنبار", "anbar", ["Anbar"]),
            ("Other Governorates", "المحافظات الأخرى", "other",
                ["Dhi Qar", "Maysan", "Wasit", "Salah ad Din", "Diyala", "Babil", "Muthanna", "Al-Qadisiyyah"]),
        };

        foreach (var (en, ar, key, gov) in zones)
        {
            var z = new ShippingZone
            {
                NameEn = en,
                NameAr = ar,
                Governorates = ShippingZoneGovernorates.Serialize(gov),
                IsActive = true,
                CreatedAt = Now,
            };
            db.ShippingZones.Add(z);
            await db.SaveChangesAsync();
            refs.ShippingZones[key] = z.Id;
        }
        logger.LogInformation("Seeded {Count} shipping zones", zones.Length);
    }

    private static async Task SeedPaymentMethodsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.PaymentMethods.AnyAsync())
        {
            await LoadRefsAsync(db, db.PaymentMethods, refs.PaymentMethods, p => p.Name, p => p.Id);
            return;
        }

        var methods = new (string Name, string Type, string LabelEn, string LabelAr, int Sort)[]
        {
            ("cod", "offline", "Cash on Delivery", "الدفع عند التسليم", 0),
            ("bank_transfer", "offline", "Bank Transfer", "تحويل مصرفي", 1),
            ("zain_cash", "online", "Zain Cash", "زين كاش", 2),
            ("asia_hawala", "offline", "Asia Hawala", "آسيا حوالة", 3),
        };

        foreach (var (name, type, labelEn, labelAr, sort) in methods)
        {
            var m = new PaymentMethod
            {
                Name = name,
                Type = type,
                LabelEn = labelEn,
                LabelAr = labelAr,
                IsActive = true,
                SortOrder = sort,
                CreatedAt = Now,
            };
            db.PaymentMethods.Add(m);
            await db.SaveChangesAsync();
            refs.PaymentMethods[name] = m.Id;
        }
        logger.LogInformation("Seeded {Count} payment methods", methods.Length);
    }

    private static async Task SeedSuppliersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Suppliers.AnyAsync())
        {
            await LoadRefsAsync(db, db.Suppliers, refs.Suppliers, s => s.Name, s => s.Id);
            return;
        }

        var suppliers = new (string Name, string Company, string Country, string City, string Currency)[]
        {
            ("Baghdad Electronics Supplier", "Baghdad Electronics Co.", "Iraq", "Baghdad", "IQD"),
            ("Dubai Tech Import Co.", "Dubai Tech Import LLC", "UAE", "Dubai", "USD"),
            ("China Direct Wholesale", "Shenzhen Tech Wholesale", "China", "Shenzhen", "USD"),
        };

        foreach (var (name, company, country, city, currency) in suppliers)
        {
            var s = new Supplier
            {
                Name = name,
                CompanyName = company,
                Country = country,
                City = city,
                Email = $"{name.Split(' ')[0].ToLower()}@supplier.com",
                Phone = "+964 770 000 0001",
                Currency = currency,
                IsActive = true,
                CreatedAt = Now,
            };
            db.Suppliers.Add(s);
            await db.SaveChangesAsync();
            refs.Suppliers[name] = s.Id;
        }
        logger.LogInformation("Seeded {Count} suppliers", suppliers.Length);
    }

    private static async Task SeedUsersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            refs.UserEntities.AddRange(await db.Users.AsNoTracking().ToListAsync());
            foreach (var u in refs.UserEntities) refs.Users[u.Email] = u.Id;
            return;
        }

        var users = new (string First, string Last, string Email, string Phone, bool Marketing)[]
        {
            ("Omar", "Hassan", "omar.hassan@gmail.com", "+964 770 123 4567", true),
            ("Fatima", "Ali", "fatima.ali@gmail.com", "+964 771 234 5678", false),
            ("Hussein", "Mahdi", "hussein.mahdi@gmail.com", "+964 772 345 6789", true),
            ("Zainab", "Kareem", "zainab.kareem@gmail.com", "+964 773 456 7890", true),
            ("Mustafa", "Jawad", "mustafa.jawad@gmail.com", "+964 774 567 8901", false),
            ("Noor", "Salim", "noor.salim@gmail.com", "+964 775 678 9012", true),
            ("Ahmed", "Rashid", "ahmed.rashid@gmail.com", "+964 776 789 0123", false),
            ("Layla", "Nouri", "layla.nouri@gmail.com", "+964 777 890 1234", true),
            ("Khalid", "Fadhil", "khalid.fadhil@gmail.com", "+964 778 901 2345", false),
            ("Sara", "Younis", "sara.younis@gmail.com", "+964 779 012 3456", true),
            ("Youssef", "Hamza", "youssef.hamza@gmail.com", "+964 780 123 4567", false),
            ("Mariam", "Tariq", "mariam.tariq@gmail.com", "+964 781 234 5678", true),
            ("Bilal", "Saad", "bilal.saad@gmail.com", "+964 782 345 6789", false),
            ("Rana", "Adnan", "rana.adnan@gmail.com", "+964 783 456 7890", true),
            ("Tariq", "Waleed", "tariq.waleed@gmail.com", "+964 784 567 8901", false),
        };

        foreach (var (first, last, email, phone, marketing) in users)
        {
            var u = new User
            {
                Email = email,
                FirstName = first,
                LastName = last,
                Phone = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedAdminPassword),
                AcceptsMarketing = marketing,
                IsActive = true,
                EmailVerifiedAt = Now,
                StoreCredit = 0,
                CreatedAt = Now,
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            refs.Users[email] = u.Id;
            refs.UserEntities.Add(u);
        }
        logger.LogInformation("Seeded {Count} users", users.Length);
    }

    private static async Task SeedPagesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Pages.AnyAsync())
        {
            await LoadRefsAsync(db, db.Pages, refs.Pages, p => p.Slug, p => p.Id);
            return;
        }

        var pages = new (string En, string Ar, string Slug)[]
        {
            ("About Us", "من نحن", "about-us"),
            ("Privacy Policy", "سياسة الخصوصية", "privacy-policy"),
            ("Terms of Service", "شروط الخدمة", "terms-of-service"),
            ("Shipping Policy", "سياسة الشحن", "shipping-policy"),
            ("Return Policy", "سياسة الاسترجاع", "return-policy"),
        };

        foreach (var (en, ar, slug) in pages)
        {
            var p = new Page
            {
                TitleEn = en,
                TitleAr = ar,
                Slug = slug,
                ContentEn = $"Content for {en} page.",
                ContentAr = $"محتوى صفحة {ar}.",
                IsPublished = true,
                CreatedAt = Now,
            };
            db.Pages.Add(p);
            await db.SaveChangesAsync();
            refs.Pages[slug] = p.Id;
        }
        logger.LogInformation("Seeded {Count} pages", pages.Length);
    }

    private static async Task SeedSettingsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Settings.AnyAsync()) return;

        var settings = new (string Key, string? Value, string Type, string? Group)[]
        {
            ("site_name", "Subul Electronics", "string", "general"),
            ("site_description_en", "Iraq's trusted online electronics store", "string", "general"),
            ("site_description_ar", "متجر إلكترونيات موثوق في العراق", "string", "general"),
            ("currency", "IQD", "string", "general"),
            ("contact_email", "store@subul.iq", "string", "contact"),
            ("contact_phone", "+964 774 802 5119", "string", "contact"),
            ("address_en", "Al-Sinaa Street, Baghdad, Iraq", "string", "contact"),
            ("address_ar", "شارع الصناعة، بغداد، العراق", "string", "contact"),
            ("social_facebook", "https://facebook.com/subul", "string", "social"),
            ("social_instagram", "https://instagram.com/subul", "string", "social"),
            ("free_shipping_threshold", "100000", "number", "shipping"),
            ("min_order_amount", "25000", "number", "orders"),
            ("tax_rate", "0", "number", "orders"),
            ("meta_title", "Subul | Iraq Electronics Store", "string", "seo"),
            ("meta_description", "Shop laptops, monitors, peripherals and more with delivery across Iraq.", "string", "seo"),
            ("whatsapp_number", "+964 774 802 5119", "string", "contact"),
            ("support_hours_en", "Sat-Thu 9AM-6PM", "string", "contact"),
            ("support_hours_ar", "السبت-الخميس 9ص-6م", "string", "contact"),
            ("warranty_default_months", "12", "number", "products"),
            ("store_since", "1989", "string", "general"),
        };

        foreach (var (key, value, type, group) in settings)
        {
            db.Settings.Add(new Setting { Key = key, Value = value, Type = type, Group = group, CreatedAt = Now });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} settings", settings.Length);
    }

    // ── Tier 2 ──────────────────────────────────────────────────────────────

    private static async Task SeedShippingRatesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ShippingRates.AnyAsync()) return;

        var rates = new (string Zone, decimal Price, int MinDays, int MaxDays, decimal? FreeThreshold)[]
        {
            ("baghdad", 5000, 1, 2, 100000),
            ("basra", 7500, 2, 3, null),
            ("erbil", 8000, 2, 3, null),
            ("mosul", 8000, 2, 4, null),
            ("najaf", 7000, 2, 3, null),
            ("karbala", 7000, 2, 3, null),
            ("sulaymaniyah", 9000, 2, 4, null),
            ("kirkuk", 8500, 2, 4, null),
            ("anbar", 10000, 3, 5, null),
            ("other", 12000, 3, 6, null),
        };

        foreach (var (zone, price, minDays, maxDays, freeThreshold) in rates)
        {
            db.ShippingRates.Add(new ShippingRate
            {
                ShippingZoneId = refs.ShippingZones[zone],
                NameEn = "Standard Delivery",
                NameAr = "توصيل عادي",
                RateType = "flat",
                Price = price,
                FreeShippingThreshold = freeThreshold,
                EstimatedDaysMin = minDays,
                EstimatedDaysMax = maxDays,
                IsActive = true,
                CreatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} shipping rates", rates.Length);
    }

    private static async Task SeedProductsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Products.AnyAsync())
        {
            refs.ProductEntities.AddRange(await db.Products.AsNoTracking().ToListAsync());
            foreach (var p in refs.ProductEntities) refs.Products[p.Slug] = p.Id;
            return;
        }

        var products = new (string Slug, string NameEn, string NameAr, string? Brand, string Category, string Sku,
            decimal Price, decimal? CompareAt, int Stock, int Warranty, bool Featured, string DescEn, string DescAr)[]
        {
            ("asus-tuf-gaming-f16-fx608jh", "ASUS TUF Gaming F16 FX608JH", "أسوس تي يو إف جيمنج إف16", "asus", "gaming-laptops", "ASUS-FX608JH", 1625000, 1750000m, 12, 12, true,
                "16\" WUXGA 165Hz gaming laptop with Intel Core i5-13450HX, 16GB RAM, 512GB SSD, RTX 5050 8GB.",
                "لابتوب ألعاب 16 بوصة WUXGA 165Hz مع معالج Intel Core i5-13450HX و16GB RAM و512GB SSD وRTX 5050 8GB."),
            ("dell-pro-15-essential-pv15250", "Dell Pro 15 Essential PV15250", "ديل برو 15 إسنشيال", "dell", "business-laptops", "DELL-PV15250", 650000, 700000m, 20, 12, true,
                "15.6\" FHD business laptop with Intel Core 3 100U, 8GB RAM, 512GB SSD.",
                "لابتوب أعمال 15.6 بوصة FHD مع Intel Core 3 100U و8GB RAM و512GB SSD."),
            ("lenovo-ideapad-slim-3-15iru8", "Lenovo IdeaPad Slim 3 15IRU8", "لينوفو آيديا باد سليم 3", "lenovo", "business-laptops", "LEN-15IRU8", 650000, null, 18, 12, true,
                "15.6\" laptop with Intel Core i3-1315U, 8GB RAM, 256GB SSD.",
                "لابتوب 15.6 بوصة مع Intel Core i3-1315U و8GB RAM و256GB SSD."),
            ("msi-cyborg-15-b13wfkg", "MSI Cyborg 15 B13WFKG", "إم إس آي سايبورغ 15", "msi", "gaming-laptops", "MSI-B13WFKG", 1750000, null, 8, 12, true,
                "15.6\" FHD 144Hz gaming laptop with Core i7-13620H, 16GB RAM, RTX 5060 8GB.",
                "لابتوب ألعاب 15.6 بوصة FHD 144Hz مع Core i7-13620H و16GB RAM وRTX 5060 8GB."),
            ("hp-laptop-15-fd0000ne", "HP Laptop 15-fd0000ne", "إتش بي لابتوب 15", "hp", "business-laptops", "HP-FD0000NE", 975000, 1100000m, 15, 12, true,
                "15.6\" FHD laptop with Core i7-1355U, 8GB RAM, 512GB SSD, includes bag and mouse.",
                "لابتوب 15.6 بوصة FHD مع Core i7-1355U و8GB RAM و512GB SSD مع حقيبة وفأرة."),
            ("asus-rog-strix-g16-g615jpr", "ASUS ROG Strix G16 G615JPR", "أسوس روج ستريكس جي16", "asus", "gaming-laptops", "ASUS-G615JPR", 2550000, null, 6, 12, true,
                "16\" 165Hz gaming laptop with Core i7-14650HX, RTX 5070 8GB, 16GB RAM, 512GB SSD.",
                "لابتوب ألعاب 16 بوصة 165Hz مع Core i7-14650HX وRTX 5070 8GB و16GB RAM و512GB SSD."),
            ("hp-p24v-g5-monitor", "HP P24v G5 23.8\" Monitor", "شاشة إتش بي P24v G5 23.8 بوصة", "hp", "business-monitors", "HP-P24V-G5", 185000, 210000m, 25, 12, false,
                "23.8\" FHD business monitor with crisp visuals and adjustable tilt.",
                "شاشة أعمال 23.8 بوصة FHD برؤية واضحة وقاعدة قابلة للتعديل."),
            ("msi-g321q-monitor", "MSI G321Q 31.5\" WQHD Monitor", "شاشة إم إس آي G321Q 31.5 بوصة", "msi", "gaming-monitors", "MSI-G321Q", 420000, 460000m, 10, 12, false,
                "31.5\" WQHD IPS gaming monitor, 170Hz, 1ms, FreeSync, HDR.",
                "شاشة ألعاب 31.5 بوصة WQHD IPS، 170Hz، 1ms، FreeSync، HDR."),
            ("samsung-essential-s3-s30gd", "Samsung Essential S3 S30GD 27\"", "سامسونج إسنشيال S3 27 بوصة", "samsung", "business-monitors", "SAM-S30GD", 200000, 225000m, 14, 12, false,
                "27\" FHD IPS monitor, 100Hz, 5ms, VESA mount, HDMI and VGA.",
                "شاشة 27 بوصة FHD IPS، 100Hz، 5ms، تثبيت VESA، HDMI وVGA."),
            ("dell-se2425h-monitor", "Dell SE2425H 24\" Monitor", "شاشة ديل SE2425H 24 بوصة", "dell", "business-monitors", "DELL-SE2425H", 175000, null, 22, 12, false,
                "24\" Full HD monitor, 75Hz, VA panel, HDMI and VGA.",
                "شاشة 24 بوصة Full HD، 75Hz، لوحة VA، HDMI وVGA."),
            ("logitech-mx-keys-keyboard", "Logitech MX Keys Advanced Keyboard", "لوحة مفاتيح لوجيتك MX Keys", "logitech", "keyboards", "LOG-MXKEYS", 185000, 200000m, 30, 6, false,
                "Premium wireless keyboard with backlit keys and multi-device connectivity.",
                "لوحة مفاتيح لاسلكية فاخرة مع إضاءة خلفية واتصال متعدد الأجهزة."),
            ("logitech-g502-x-mouse", "Logitech G502 X Gaming Mouse", "فأرة لوجيتك G502 X للألعاب", "logitech", "mice", "LOG-G502X", 95000, 110000m, 40, 6, false,
                "Lightweight gaming mouse with HERO sensor and customizable buttons.",
                "فأرة ألعاب خفيفة مع مستشعر HERO وأزرار قابلة للتخصيص."),
            ("logitech-g-pro-x-headset", "Logitech G Pro X Headset", "سماعة لوجيتك G Pro X", "logitech", "audio", "LOG-GPROX", 225000, 250000m, 18, 6, false,
                "Pro-grade gaming headset with Blue VO!CE mic technology.",
                "سماعة ألعاب احترافية مع تقنية ميكروفون Blue VO!CE."),
            ("kingston-nv2-1tb-ssd", "Kingston NV2 1TB SSD", "كينجستون NV2 1TB SSD", "kingston", "ssds", "KIN-NV2-1TB", 65000, 75000m, 50, 6, false,
                "NVMe PCIe 4.0 SSD, 1TB capacity, up to 3500MB/s read.",
                "قرص SSD NVMe PCIe 4.0 بسعة 1TB، سرعة قراءة حتى 3500MB/s."),
            ("kingston-fury-beast-ddr5-32gb", "Kingston FURY Beast DDR5 32GB", "كينجستون FURY بيست DDR5 32GB", "kingston", "ram", "KIN-FURY-32GB", 145000, 165000m, 35, 6, false,
                "DDR5 32GB (2x16GB) 5600MHz desktop memory kit.",
                "ذاكرة سطح مكتب DDR5 32GB (2x16GB) 5600MHz."),
            ("seagate-barracuda-2tb-hdd", "Seagate Barracuda 2TB HDD", "سيغيت باراكودا 2TB HDD", "seagate", "hdds", "SEA-BARR-2TB", 75000, 90000m, 40, 6, false,
                "2TB 7200RPM SATA internal hard drive for desktop storage.",
                "قرص صلب داخلي SATA 2TB 7200RPM لتخزين سطح المكتب."),
            ("tp-link-archer-ax6000-router", "TP-Link Archer AX6000 Router", "راوتر TP-Link Archer AX6000", null, "wireless-routers", "TPL-AX6000", 285000, 320000m, 12, 6, false,
                "Wi-Fi 6 AX6000 dual-band router with 8-stream technology.",
                "راوتر Wi-Fi 6 AX6000 ثنائي النطاق بتقنية 8-stream."),
            ("hp-laserjet-pro-m404n", "HP LaserJet Pro M404n", "طابعة إتش بي ليزرجيت برو M404n", "hp", "printers-scanners", "HP-M404N", 425000, null, 8, 12, false,
                "Monochrome laser printer, 38 ppm, Ethernet and USB connectivity.",
                "طابعة ليزر أحادية اللون، 38 صفحة/دقيقة، اتصال Ethernet وUSB."),
            ("logitech-g840-mouse-pad", "Logitech G840 XL Mouse Pad", "سجادة فأرة لوجيتك G840 XL", "logitech", "mouse-pads", "LOG-G840", 55000, null, 60, 6, false,
                "Extra-large gaming mouse pad with consistent surface texture.",
                "سجادة فأرة ألعاب كبيرة جداً بسطح متسق."),
            ("logitech-c920-webcam", "Logitech C920 HD Pro Webcam", "كاميرا ويب لوجيتك C920 HD Pro", "logitech", "webcams", "LOG-C920", 165000, 185000m, 25, 6, false,
                "Full HD 1080p webcam with stereo audio for video calls.",
                "كاميرا ويب Full HD 1080p مع صوت ستيريو للمكالمات المرئية."),
        };

        foreach (var (slug, nameEn, nameAr, brand, category, sku, price, compareAt, stock, warranty, featured, descEn, descAr) in products)
        {
            var p = new Product
            {
                NameEn = nameEn,
                NameAr = nameAr,
                Slug = slug,
                Sku = sku,
                BrandId = brand is not null ? refs.Brands[brand] : null,
                CategoryId = refs.Categories[category],
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                ShortDescriptionEn = descEn.Split('.')[0] + ".",
                ShortDescriptionAr = descAr.Split('.')[0] + ".",
                Price = price,
                CompareAtPrice = compareAt,
                CostPrice = price * 0.75m,
                Currency = "IQD",
                StockQuantity = stock,
                LowStockThreshold = 5,
                MinOrderQuantity = 1,
                Weight = 2.0m,
                Status = "active",
                IsFeatured = featured,
                RequiresShipping = true,
                WarrantyMonths = warranty,
                WarrantyDescription = warranty >= 12 ? "1-year manufacturer warranty" : "6-month warranty",
                TotalSold = Random.Shared.Next(5, 120),
                ViewsCount = Random.Shared.Next(50, 2000),
                MetaTitle = nameEn,
                MetaDescription = descEn,
                CreatedAt = Now,
            };
            db.Products.Add(p);
            await db.SaveChangesAsync();
            refs.Products[slug] = p.Id;
            refs.ProductEntities.Add(p);
        }
        logger.LogInformation("Seeded {Count} products", products.Length);
    }

    private static async Task SeedDeliveryAgentsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.DeliveryAgents.AnyAsync())
        {
            await LoadRefsAsync(db, db.DeliveryAgents, refs.DeliveryAgents, a => a.Name, a => a.Id);
            return;
        }

        var agents = new (string Name, string Phone, string Area)[]
        {
            ("Hassan Al-Baghdadi", "+964 770 111 2233", "Baghdad"),
            ("Karim Al-Basrawi", "+964 771 222 3344", "Basra"),
            ("Samir Al-Mosuli", "+964 772 333 4455", "North Iraq"),
        };

        foreach (var (name, phone, area) in agents)
        {
            var a = new DeliveryAgent { Name = name, Phone = phone, Whatsapp = phone, Area = area, IsActive = true, CreatedAt = Now };
            db.DeliveryAgents.Add(a);
            await db.SaveChangesAsync();
            refs.DeliveryAgents[name] = a.Id;
        }
        logger.LogInformation("Seeded {Count} delivery agents", agents.Length);
    }

    private static async Task SeedDiscountsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Discounts.AnyAsync())
        {
            await LoadRefsAsync(db, db.Discounts, refs.Discounts, d => d.Code!, d => d.Id);
            return;
        }

        var discounts = new (string Code, string Name, string Type, decimal Value, decimal? MinOrder, bool Active)[]
        {
            ("WELCOME10", "Welcome 10% Off", "percentage", 10, 100000, true),
            ("GAMING15", "Gaming Laptops 15% Off", "percentage", 15, 1000000, true),
            ("SUMMER50K", "Summer 50K IQD Off", "fixed_amount", 50000, 500000, true),
        };

        foreach (var (code, name, type, value, minOrder, active) in discounts)
        {
            var d = new Discount
            {
                Code = code,
                Name = name,
                Type = type,
                Value = value,
                Currency = "IQD",
                MinOrderValue = minOrder,
                UsageLimit = 1000,
                UsageCount = 0,
                PerCustomerLimit = 1,
                AppliesTo = "all",
                IsActive = active,
                StartsAt = Now.AddDays(-30),
                EndsAt = Now.AddDays(90),
                CreatedAt = Now,
            };
            db.Discounts.Add(d);
            await db.SaveChangesAsync();
            refs.Discounts[code] = d.Id;
        }
        logger.LogInformation("Seeded {Count} discounts", discounts.Length);
    }

    // ── Tier 3 ──────────────────────────────────────────────────────────────

    private static async Task SeedProductImagesAsync(
        AppDbContext db,
        SeedReferences refs,
        IWebHostEnvironment env,
        ILogger logger)
    {
        if (await db.ProductImages.AnyAsync()) return;

        var imageMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["asus-tuf-gaming-f16-fx608jh"] = ["1587202372775-e229f172b9d7", "1496181133206-80ce9b88a853"],
            ["dell-pro-15-essential-pv15250"] = ["1496181133206-80ce9b88a853", "1587202372775-e229f172b9d7"],
            ["lenovo-ideapad-slim-3-15iru8"] = ["1496181133206-80ce9b88a853", "1587202372775-e229f172b9d7"],
            ["msi-cyborg-15-b13wfkg"] = ["1587202372775-e229f172b9d7", "1496181133206-80ce9b88a853"],
            ["hp-laptop-15-fd0000ne"] = ["1496181133206-80ce9b88a853", "1587202372775-e229f172b9d7"],
            ["asus-rog-strix-g16-g615jpr"] = ["1587202372775-e229f172b9d7", "1496181133206-80ce9b88a853"],
            ["hp-p24v-g5-monitor"] = ["1527443224154-c4a3942d3acf", "1593640408182-31c70c8268f5"],
            ["msi-g321q-monitor"] = ["1527443224154-c4a3942d3acf", "1593640408182-31c70c8268f5"],
            ["samsung-essential-s3-s30gd"] = ["1593640408182-31c70c8268f5", "1527443224154-c4a3942d3acf"],
            ["dell-se2425h-monitor"] = ["1527443224154-c4a3942d3acf", "1593640408182-31c70c8268f5"],
            ["logitech-mx-keys-keyboard"] = ["1541140532154-b024d705b90a", "1541140532154-b024d705b90a"],
            ["logitech-g502-x-mouse"] = ["1527814050087-3793815479db", "1527814050087-3793815479db"],
            ["logitech-g-pro-x-headset"] = ["1505740420928-5e560c06d30e", "1505740420928-5e560c06d30e"],
            ["kingston-nv2-1tb-ssd"] = ["1597872200969-2b65d56bd16b", "1597872200969-2b65d56bd16b"],
            ["kingston-fury-beast-ddr5-32gb"] = ["1597872200969-2b65d56bd16b", "1593640408182-31c70c8268f5"],
            ["seagate-barracuda-2tb-hdd"] = ["1597872200969-2b65d56bd16b", "1597872200969-2b65d56bd16b"],
            ["tp-link-archer-ax6000-router"] = ["1544197150-b99a580bb7a8", "1544197150-b99a580bb7a8"],
            ["hp-laserjet-pro-m404n"] = ["1541140532154-b024d705b90a", "1541140532154-b024d705b90a"],
            ["logitech-g840-mouse-pad"] = ["1527814050087-3793815479db", "1527814050087-3793815479db"],
            ["logitech-c920-webcam"] = ["1505740420928-5e560c06d30e", "1505740420928-5e560c06d30e"],
        };

        var count = 0;
        foreach (var (slug, photos) in imageMap)
        {
            for (var i = 0; i < photos.Length; i++)
            {
                db.ProductImages.Add(new ProductImage
                {
                    ProductId = refs.Products[slug],
                    ImageUrl = await SeedImageDownloader.ResolveSeedPhotoAsync(env, photos[i]),
                    AltText = slug.Replace('-', ' '),
                    SortOrder = i,
                    IsPrimary = i == 0,
                    CreatedAt = Now,
                });
                count++;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} product images", count);
    }

    private static async Task SeedProductVariantsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ProductVariants.AnyAsync())
        {
            var existingVariants = await db.ProductVariants.AsNoTracking().ToListAsync();
            foreach (var v in existingVariants)
                if (v.Sku is not null) refs.ProductVariants[v.Sku] = v.Id;
            return;
        }

        var variantDefs = new (string ProductSlug, string Sku, string Title, decimal Price, int Stock)[]
        {
            ("asus-tuf-gaming-f16-fx608jh", "ASUS-FX608JH-16-512", "16GB / 512GB SSD", 1625000, 8),
            ("asus-tuf-gaming-f16-fx608jh", "ASUS-FX608JH-32-1TB", "32GB / 1TB SSD", 1890000, 4),
            ("lenovo-ideapad-slim-3-15iru8", "LEN-15IRU8-8-256", "8GB / 256GB SSD", 650000, 10),
            ("lenovo-ideapad-slim-3-15iru8", "LEN-15IRU8-8-512", "8GB / 512GB SSD", 725000, 8),
            ("kingston-nv2-1tb-ssd", "KIN-NV2-500GB", "500GB", 42000, 30),
            ("kingston-nv2-1tb-ssd", "KIN-NV2-1TB", "1TB", 65000, 50),
            ("kingston-nv2-1tb-ssd", "KIN-NV2-2TB", "2TB", 115000, 20),
            ("hp-laptop-15-fd0000ne", "HP-FD0000NE-BLUE", "Blue", 975000, 5),
            ("hp-laptop-15-fd0000ne", "HP-FD0000NE-SILVER", "Silver", 975000, 5),
            ("hp-laptop-15-fd0000ne", "HP-FD0000NE-GOLD", "Gold", 995000, 3),
            ("msi-cyborg-15-b13wfkg", "MSI-B13WFKG-16-512", "16GB / 512GB", 1750000, 6),
            ("msi-cyborg-15-b13wfkg", "MSI-B13WFKG-32-1TB", "32GB / 1TB", 1990000, 2),
            ("logitech-g502-x-mouse", "LOG-G502X-BLK", "Black", 95000, 25),
            ("logitech-g502-x-mouse", "LOG-G502X-WHT", "White", 95000, 15),
            ("asus-rog-strix-g16-g615jpr", "ASUS-G615JPR-16-512", "16GB / 512GB", 2550000, 4),
        };

        foreach (var (productSlug, sku, title, price, stock) in variantDefs)
        {
            var v = new ProductVariant
            {
                ProductId = refs.Products[productSlug],
                Sku = sku,
                Title = title,
                Price = price,
                StockQuantity = stock,
                IsActive = true,
                SortOrder = refs.ProductVariants.Count,
                CreatedAt = Now,
            };
            db.ProductVariants.Add(v);
            await db.SaveChangesAsync();
            refs.ProductVariants[sku] = v.Id;
        }
        logger.LogInformation("Seeded {Count} product variants", variantDefs.Length);
    }

    private static async Task SeedProductAttributeValuesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ProductAttributeValues.AnyAsync()) return;

        void AddText(string slug, string attr, string value) =>
            db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = refs.Products[slug],
                AttributeId = refs.Attributes[attr],
                ValueText = value,
                CreatedAt = Now,
            });

        void AddNum(string slug, string attr, decimal value) =>
            db.ProductAttributeValues.Add(new ProductAttributeValue
            {
                ProductId = refs.Products[slug],
                AttributeId = refs.Attributes[attr],
                ValueNumber = value,
                CreatedAt = Now,
            });

        // ASUS TUF Gaming F16
        AddNum("asus-tuf-gaming-f16-fx608jh", "screen-size", 16);
        AddText("asus-tuf-gaming-f16-fx608jh", "resolution", "1920x1200 WUXGA");
        AddNum("asus-tuf-gaming-f16-fx608jh", "refresh-rate", 165);
        AddText("asus-tuf-gaming-f16-fx608jh", "panel-type", "IPS");
        AddText("asus-tuf-gaming-f16-fx608jh", "processor", "Intel Core i5-13450HX");
        AddNum("asus-tuf-gaming-f16-fx608jh", "ram", 16);
        AddNum("asus-tuf-gaming-f16-fx608jh", "storage", 512);
        AddText("asus-tuf-gaming-f16-fx608jh", "gpu", "NVIDIA RTX 5050 8GB");
        AddNum("asus-tuf-gaming-f16-fx608jh", "weight", 2.2m);

        // Dell Pro 15
        AddNum("dell-pro-15-essential-pv15250", "screen-size", 15.6m);
        AddText("dell-pro-15-essential-pv15250", "resolution", "1920x1080 FHD");
        AddText("dell-pro-15-essential-pv15250", "processor", "Intel Core 3 100U");
        AddNum("dell-pro-15-essential-pv15250", "ram", 8);
        AddNum("dell-pro-15-essential-pv15250", "storage", 512);
        AddNum("dell-pro-15-essential-pv15250", "weight", 1.7m);

        // Lenovo IdeaPad
        AddNum("lenovo-ideapad-slim-3-15iru8", "screen-size", 15.6m);
        AddText("lenovo-ideapad-slim-3-15iru8", "processor", "Intel Core i3-1315U");
        AddNum("lenovo-ideapad-slim-3-15iru8", "ram", 8);
        AddNum("lenovo-ideapad-slim-3-15iru8", "storage", 256);

        // MSI Cyborg
        AddNum("msi-cyborg-15-b13wfkg", "screen-size", 15.6m);
        AddNum("msi-cyborg-15-b13wfkg", "refresh-rate", 144);
        AddText("msi-cyborg-15-b13wfkg", "processor", "Intel Core i7-13620H");
        AddNum("msi-cyborg-15-b13wfkg", "ram", 16);
        AddText("msi-cyborg-15-b13wfkg", "gpu", "NVIDIA RTX 5060 8GB");

        // HP Laptop
        AddNum("hp-laptop-15-fd0000ne", "screen-size", 15.6m);
        AddText("hp-laptop-15-fd0000ne", "processor", "Intel Core i7-1355U");
        AddNum("hp-laptop-15-fd0000ne", "ram", 8);
        AddNum("hp-laptop-15-fd0000ne", "storage", 512);
        AddText("hp-laptop-15-fd0000ne", "color", "Blue");

        // ASUS ROG Strix
        AddNum("asus-rog-strix-g16-g615jpr", "screen-size", 16);
        AddNum("asus-rog-strix-g16-g615jpr", "refresh-rate", 165);
        AddText("asus-rog-strix-g16-g615jpr", "processor", "Intel Core i7-14650HX");
        AddText("asus-rog-strix-g16-g615jpr", "gpu", "NVIDIA RTX 5070 8GB");

        // Monitors
        AddNum("hp-p24v-g5-monitor", "screen-size", 23.8m);
        AddText("hp-p24v-g5-monitor", "resolution", "1920x1080 FHD");
        AddText("hp-p24v-g5-monitor", "panel-type", "IPS");
        AddNum("msi-g321q-monitor", "screen-size", 31.5m);
        AddText("msi-g321q-monitor", "resolution", "2560x1440 WQHD");
        AddNum("msi-g321q-monitor", "refresh-rate", 170);
        AddNum("samsung-essential-s3-s30gd", "screen-size", 27);
        AddNum("samsung-essential-s3-s30gd", "refresh-rate", 100);
        AddNum("dell-se2425h-monitor", "screen-size", 24);

        // Peripherals & storage
        AddText("logitech-mx-keys-keyboard", "connectivity-type", "Wireless / Bluetooth");
        AddText("logitech-mx-keys-keyboard", "color", "Graphite");
        AddNum("logitech-g502-x-mouse", "dpi", 25600);
        AddText("logitech-g-pro-x-headset", "connectivity-type", "Wired");
        AddNum("kingston-nv2-1tb-ssd", "capacity-storage", 1024);
        AddNum("kingston-fury-beast-ddr5-32gb", "ram", 32);
        AddNum("seagate-barracuda-2tb-hdd", "capacity-storage", 2048);
        AddText("tp-link-archer-ax6000-router", "wifi-standard", "Wi-Fi 6 AX6000");
        AddNum("hp-laserjet-pro-m404n", "print-speed", 38);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded product attribute values");
    }

    private static async Task SeedProductTagsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ProductTags.AnyAsync()) return;

        var mappings = new (string Slug, string[] Tags)[]
        {
            ("asus-tuf-gaming-f16-fx608jh", ["gaming", "laptop"]),
            ("dell-pro-15-essential-pv15250", ["laptop", "portable"]),
            ("lenovo-ideapad-slim-3-15iru8", ["laptop", "budget"]),
            ("msi-cyborg-15-b13wfkg", ["gaming", "laptop"]),
            ("hp-laptop-15-fd0000ne", ["laptop", "portable"]),
            ("asus-rog-strix-g16-g615jpr", ["gaming", "laptop"]),
            ("hp-p24v-g5-monitor", ["monitor", "ips"]),
            ("msi-g321q-monitor", ["monitor", "gaming", "4k"]),
            ("samsung-essential-s3-s30gd", ["monitor", "ips"]),
            ("dell-se2425h-monitor", ["monitor", "budget"]),
            ("logitech-mx-keys-keyboard", ["keyboard", "wireless"]),
            ("logitech-g502-x-mouse", ["mouse", "gaming", "rgb"]),
            ("logitech-g-pro-x-headset", ["headset", "gaming"]),
            ("kingston-nv2-1tb-ssd", ["ssd"]),
            ("kingston-fury-beast-ddr5-32gb", ["rgb"]),
            ("seagate-barracuda-2tb-hdd", ["hdd"]),
            ("tp-link-archer-ax6000-router", ["wireless"]),
            ("hp-laserjet-pro-m404n", ["portable"]),
            ("logitech-g840-mouse-pad", ["gaming"]),
            ("logitech-c920-webcam", ["wireless"]),
        };

        var count = 0;
        foreach (var (slug, tags) in mappings)
        {
            foreach (var tag in tags)
            {
                db.ProductTags.Add(new ProductTag
                {
                    ProductId = refs.Products[slug],
                    TagId = refs.Tags[tag],
                    CreatedAt = Now,
                });
                count++;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} product tags", count);
    }

    private static async Task SeedCollectionsAsync(
        AppDbContext db,
        SeedReferences refs,
        IWebHostEnvironment env,
        ILogger logger)
    {
        if (await db.Collections.AnyAsync())
        {
            await LoadRefsAsync(db, db.Collections, refs.Collections, c => c.Slug, c => c.Id);
            return;
        }

        var collections = new (string En, string Ar, string Slug, string Image)[]
        {
            ("Best Sellers", "الأكثر مبيعاً", "best-sellers", "1587202372775-e229f172b9d7"),
            ("New Arrivals", "وصل حديثاً", "new-arrivals", "1496181133206-80ce9b88a853"),
            ("Gaming Setup", "إعداد الألعاب", "gaming-setup", "1593305841991-05c297ba4575"),
        };

        foreach (var (en, ar, slug, img) in collections)
        {
            var c = new Collection
            {
                NameEn = en,
                NameAr = ar,
                Slug = slug,
                DescriptionEn = $"Curated {en} collection.",
                DescriptionAr = $"مجموعة {ar} مختارة.",
                ImageUrl = await SeedImageDownloader.ResolveSeedPhotoAsync(env, img),
                CollectionType = "manual",
                IsActive = true,
                SortOrder = refs.Collections.Count,
                CreatedAt = Now,
            };
            db.Collections.Add(c);
            await db.SaveChangesAsync();
            refs.Collections[slug] = c.Id;
        }
        logger.LogInformation("Seeded {Count} collections", collections.Length);
    }

    private static async Task SeedBannersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Banners.AnyAsync()) return;

        var banners = new (string TitleEn, string TitleAr, string Image, string? Link, int Sort)[]
        {
            ("ASUS TUF Gaming", "أسوس تي يو إف جيمنج", "1587202372775-e229f172b9d7", "/collections/gaming-setup", 0),
            ("Gaming Laptops", "لابتوبات الألعاب", "1496181133206-80ce9b88a853", "/categories/gaming-laptops", 1),
            ("Monitors Sale", "تخفيضات الشاشات", "1527443224154-c4a3942d3acf", "/categories/monitors", 2),
            ("Free Shipping 100K+", "شحن مجاني فوق 100,000", "1558618666-fcd25c85cd64", null, 3),
            ("Logitech Peripherals", "ملحقات لوجيتك", "1541140532154-b024d705b90a", "/categories/peripherals", 4),
        };

        foreach (var (titleEn, titleAr, image, link, sort) in banners)
        {
            db.Banners.Add(new Banner
            {
                TitleEn = titleEn,
                TitleAr = titleAr,
                SubtitleEn = "Shop now at Subul",
                SubtitleAr = "تسوق الآن من سبل",
                ImageUrl = Unsplash(image),
                LinkUrl = link,
                ButtonTextEn = "Shop Now",
                ButtonTextAr = "تسوق الآن",
                Position = "home",
                SortOrder = sort,
                StartsAt = Now.AddDays(-7),
                EndsAt = Now.AddDays(60),
                IsActive = true,
                CreatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} banners", banners.Length);
    }

    private static async Task SeedMenusAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Menus.AnyAsync())
        {
            await LoadRefsAsync(db, db.Menus, refs.Menus, m => m.Name, m => m.Id);
            return;
        }

        var main = new Menu { Name = "main-navigation", LabelEn = "Main Navigation", LabelAr = "القائمة الرئيسية", IsActive = true, CreatedAt = Now };
        var footer = new Menu { Name = "footer-navigation", LabelEn = "Footer Navigation", LabelAr = "قائمة التذييل", IsActive = true, CreatedAt = Now };
        db.Menus.AddRange(main, footer);
        await db.SaveChangesAsync();
        refs.Menus["main-navigation"] = main.Id;
        refs.Menus["footer-navigation"] = footer.Id;
        logger.LogInformation("Seeded 2 menus");
    }

    private static async Task SeedAddressesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Addresses.AnyAsync())
        {
            var addresses = await db.Addresses.AsNoTracking().ToListAsync();
            foreach (var a in addresses) refs.Addresses[$"user-{a.UserId}"] = a.Id;
            return;
        }

        var governorates = new[] { "Baghdad", "Basra", "Erbil", "Mosul", "Najaf", "Karbala", "Sulaymaniyah", "Kirkuk", "Anbar", "Dhi Qar", "Wasit", "Diyala", "Babil", "Maysan", "Salah ad Din" };
        var i = 0;
        foreach (var user in refs.UserEntities)
        {
            var gov = governorates[i % governorates.Length];
            var a = new Address
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                Address1 = $"{10 + i} Al-Rasheed Street",
                Address2 = $"Building {i + 1}",
                City = gov == "Baghdad" ? "Baghdad" : gov,
                Governorate = gov,
                Country = "IQ",
                IsDefault = true,
                CreatedAt = Now,
            };
            db.Addresses.Add(a);
            await db.SaveChangesAsync();
            refs.Addresses[$"user-{user.Id}"] = a.Id;
            i++;
        }
        logger.LogInformation("Seeded {Count} addresses", refs.UserEntities.Count);
    }

    private static async Task SeedCartsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Carts.AnyAsync())
        {
            var carts = await db.Carts.AsNoTracking().ToListAsync();
            foreach (var c in carts)
                refs.Carts[c.SessionId ?? $"user-{c.UserId}"] = c.Id;
            return;
        }

        for (var i = 0; i < 5; i++)
        {
            var user = refs.UserEntities[i];
            var cart = new Cart
            {
                UserId = user.Id,
                SessionId = $"seed-session-{i + 1}",
                CreatedAt = Now,
                UpdatedAt = Now,
            };
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
            refs.Carts[cart.SessionId!] = cart.Id;
        }
        logger.LogInformation("Seeded 5 carts");
    }

    // ── Tier 4 ──────────────────────────────────────────────────────────────

    private static async Task SeedCollectionProductsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.CollectionProducts.AnyAsync()) return;

        var mappings = new (string Collection, string[] Products)[]
        {
            ("best-sellers", ["asus-tuf-gaming-f16-fx608jh", "dell-pro-15-essential-pv15250", "lenovo-ideapad-slim-3-15iru8", "hp-p24v-g5-monitor", "msi-g321q-monitor"]),
            ("new-arrivals", ["msi-cyborg-15-b13wfkg", "hp-laptop-15-fd0000ne", "asus-rog-strix-g16-g615jpr"]),
            ("gaming-setup", ["asus-tuf-gaming-f16-fx608jh", "msi-cyborg-15-b13wfkg", "asus-rog-strix-g16-g615jpr", "msi-g321q-monitor", "logitech-g502-x-mouse", "logitech-g-pro-x-headset"]),
        };

        var sort = 0;
        foreach (var (collection, products) in mappings)
        {
            sort = 0;
            foreach (var slug in products)
            {
                db.CollectionProducts.Add(new CollectionProduct
                {
                    CollectionId = refs.Collections[collection],
                    ProductId = refs.Products[slug],
                    SortOrder = sort++,
                    CreatedAt = Now,
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded collection products");
    }

    private static async Task SeedCartItemsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.CartItems.AnyAsync()) return;

        var items = new (int CartIndex, string ProductSlug, int Qty)[]
        {
            (0, "asus-tuf-gaming-f16-fx608jh", 1),
            (0, "logitech-g502-x-mouse", 2),
            (1, "kingston-nv2-1tb-ssd", 1),
            (1, "logitech-mx-keys-keyboard", 1),
            (2, "msi-g321q-monitor", 1),
            (3, "hp-p24v-g5-monitor", 1),
            (3, "logitech-c920-webcam", 1),
            (4, "dell-pro-15-essential-pv15250", 1),
        };

        foreach (var (cartIndex, slug, qty) in items)
        {
            var product = refs.ProductEntities.First(p => p.Slug == slug);
            db.CartItems.Add(new CartItem
            {
                CartId = refs.Carts[$"seed-session-{cartIndex + 1}"],
                ProductId = product.Id,
                Quantity = qty,
                UnitPrice = product.Price,
                CreatedAt = Now,
                UpdatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} cart items", items.Length);
    }

    private static async Task SeedOrdersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Orders.AnyAsync())
        {
            refs.OrderEntities.AddRange(await db.Orders.AsNoTracking().OrderBy(o => o.Id).ToListAsync());
            foreach (var o in refs.OrderEntities) refs.Orders[o.OrderNumber] = o.Id;
            return;
        }

        var statuses = new[]
        {
            "delivered", "delivered", "delivered", "delivered", "delivered", "delivered", "delivered", "delivered",
            "shipped", "shipped", "shipped", "shipped",
            "processing", "processing", "processing",
            "pending", "pending", "pending",
            "cancelled", "cancelled",
        };

        var paymentMethods = new[] { "cod", "bank_transfer", "zain_cash", "cod" };

        for (var i = 0; i < 20; i++)
        {
            var user = refs.UserEntities[i % refs.UserEntities.Count];
            var address = await db.Addresses.AsNoTracking().FirstAsync(a => a.UserId == user.Id);
            var status = statuses[i];
            var subtotal = refs.ProductEntities[i % refs.ProductEntities.Count].Price
                         + refs.ProductEntities[(i + 1) % refs.ProductEntities.Count].Price;
            var shipping = subtotal >= 100000 ? 0 : 5000;
            var total = subtotal + shipping;

            var order = new Order
            {
                UserId = user.Id,
                OrderNumber = $"ORD-2026-{10001 + i:D5}",
                Status = status,
                PaymentStatus = status is "delivered" or "shipped" ? "paid" : "pending",
                FulfillmentStatus = status == "delivered" ? "fulfilled" : status == "shipped" ? "partial" : "unfulfilled",
                Subtotal = subtotal,
                DiscountAmount = 0,
                ShippingAmount = shipping,
                TaxAmount = 0,
                Total = total,
                Currency = "IQD",
                ShippingFirstName = address.FirstName,
                ShippingLastName = address.LastName,
                ShippingPhone = address.Phone,
                ShippingAddress1 = address.Address1,
                ShippingAddress2 = address.Address2,
                ShippingCity = address.City,
                ShippingGovernorate = address.Governorate,
                ShippingCountry = "IQ",
                ShippingZoneId = refs.ShippingZones["baghdad"],
                PaymentMethod = paymentMethods[i % paymentMethods.Length],
                TrackingNumber = status is "shipped" or "delivered" ? $"TRK-{1000 + i}" : null,
                ShippedAt = status is "shipped" or "delivered" ? Now.AddDays(-3) : null,
                DeliveredAt = status == "delivered" ? Now.AddDays(-1) : null,
                CancelledAt = status == "cancelled" ? Now.AddDays(-2) : null,
                CancelReason = status == "cancelled" ? "Customer requested cancellation" : null,
                DiscountId = i < 5 ? refs.Discounts["WELCOME10"] : null,
                CouponCode = i < 5 ? "WELCOME10" : null,
                CreatedAt = Now.AddDays(-(20 - i)),
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            refs.Orders[order.OrderNumber] = order.Id;
            refs.OrderEntities.Add(order);
        }
        logger.LogInformation("Seeded 20 orders");
    }

    private static async Task SeedFlashSalesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.FlashSales.AnyAsync())
        {
            await LoadRefsAsync(db, db.FlashSales, refs.FlashSales, f => f.NameEn, f => f.Id);
            return;
        }

        var sale = new FlashSale
        {
            NameEn = "Summer Gaming Sale",
            NameAr = "تخفيضات الألعاب الصيفية",
            DescriptionEn = "15% off selected gaming products.",
            DescriptionAr = "خصم 15% على منتجات الألعاب المختارة.",
            BannerUrl = Unsplash("1593305841991-05c297ba4575"),
            DiscountType = "percentage",
            DiscountValue = 15,
            StartsAt = Now.AddDays(-1),
            EndsAt = Now.AddDays(2),
            IsActive = true,
            SortOrder = 0,
            CreatedBy = refs.AdminUsers["admin@subul.iq"],
            CreatedAt = Now,
        };
        db.FlashSales.Add(sale);
        await db.SaveChangesAsync();
        refs.FlashSales["Summer Gaming Sale"] = sale.Id;
        logger.LogInformation("Seeded 1 flash sale");
    }

    private static async Task SeedPurchaseOrdersAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.PurchaseOrders.AnyAsync())
        {
            await LoadRefsAsync(db, db.PurchaseOrders, refs.PurchaseOrders, p => p.PoNumber, p => p.Id);
            return;
        }

        var pos = new (string PoNumber, string Supplier, string Status, string Currency, decimal Total, string PaymentStatus)[]
        {
            ("PO-2026-00001", "Baghdad Electronics Supplier", "received", "IQD", 8500000, "paid"),
            ("PO-2026-00002", "Dubai Tech Import Co.", "pending", "USD", 12000, "pending"),
        };

        foreach (var (poNumber, supplier, status, currency, total, paymentStatus) in pos)
        {
            var po = new PurchaseOrder
            {
                SupplierId = refs.Suppliers[supplier],
                PoNumber = poNumber,
                Status = status,
                OrderDate = DateOnly.FromDateTime(Now.AddDays(-14)),
                ExpectedDate = DateOnly.FromDateTime(Now.AddDays(7)),
                ReceivedDate = status == "received" ? DateOnly.FromDateTime(Now.AddDays(-3)) : null,
                Subtotal = total,
                TaxAmount = 0,
                ShippingCost = 0,
                Total = total,
                Currency = currency,
                ExchangeRate = currency == "USD" ? 1310 : 1,
                PaymentStatus = paymentStatus,
                PaidAmount = paymentStatus == "paid" ? total : 0,
                AdminUserId = refs.AdminUsers["admin@subul.iq"],
                CreatedAt = Now.AddDays(-14),
            };
            db.PurchaseOrders.Add(po);
            await db.SaveChangesAsync();
            refs.PurchaseOrders[poNumber] = po.Id;
        }
        logger.LogInformation("Seeded 2 purchase orders");
    }

    private static async Task SeedMenuItemsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.MenuItems.AnyAsync()) return;

        var mainItems = new (string En, string Ar, string LinkType, string? Slug, int Sort)[]
        {
            ("Laptops", "لابتوبات", "category", "laptops", 0),
            ("Monitors", "شاشات", "category", "monitors", 1),
            ("Peripherals", "ملحقات", "category", "peripherals", 2),
            ("Storage", "التخزين", "category", "storage", 3),
            ("Networking", "الشبكات", "category", "networking", 4),
        };

        foreach (var (en, ar, linkType, slug, sort) in mainItems)
        {
            db.MenuItems.Add(new MenuItem
            {
                MenuId = refs.Menus["main-navigation"],
                LabelEn = en,
                LabelAr = ar,
                LinkType = linkType,
                LinkId = slug is not null ? refs.Categories[slug] : null,
                Target = "_self",
                SortOrder = sort,
                IsActive = true,
                CreatedAt = Now,
            });
        }

        var footerItems = new (string En, string Ar, string PageSlug, int Sort)[]
        {
            ("About Us", "من نحن", "about-us", 0),
            ("Privacy Policy", "سياسة الخصوصية", "privacy-policy", 1),
            ("Shipping Policy", "سياسة الشحن", "shipping-policy", 2),
            ("Return Policy", "سياسة الاسترجاع", "return-policy", 3),
            ("Contact", "اتصل بنا", "about-us", 4),
        };

        foreach (var (en, ar, pageSlug, sort) in footerItems)
        {
            db.MenuItems.Add(new MenuItem
            {
                MenuId = refs.Menus["footer-navigation"],
                LabelEn = en,
                LabelAr = ar,
                LinkType = "page",
                LinkId = refs.Pages[pageSlug],
                Target = "_self",
                SortOrder = sort,
                IsActive = true,
                CreatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded menu items");
    }

    // ── Tier 5 ──────────────────────────────────────────────────────────────

    private static async Task SeedOrderItemsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.OrderItems.AnyAsync())
        {
            var items = await db.OrderItems.AsNoTracking().ToListAsync();
            for (var i = 0; i < items.Count; i++)
                refs.OrderItems[$"item-{items[i].Id}"] = items[i].Id;
            return;
        }

        var itemIndex = 0;
        foreach (var order in refs.OrderEntities)
        {
            var p1 = refs.ProductEntities[itemIndex % refs.ProductEntities.Count];
            var p2 = refs.ProductEntities[(itemIndex + 1) % refs.ProductEntities.Count];

            var items = new[]
            {
                new OrderItem
                {
                    OrderId = order.Id, ProductId = p1.Id, ProductName = p1.NameEn,
                    Sku = p1.Sku, Quantity = 1, UnitPrice = p1.Price,
                    CompareAtPrice = p1.CompareAtPrice, DiscountAmount = 0,
                    TotalPrice = p1.Price, WarrantyMonths = p1.WarrantyMonths,
                    RequiresShipping = true, CreatedAt = order.CreatedAt,
                },
                new OrderItem
                {
                    OrderId = order.Id, ProductId = p2.Id, ProductName = p2.NameEn,
                    Sku = p2.Sku, Quantity = 1, UnitPrice = p2.Price,
                    DiscountAmount = 0, TotalPrice = p2.Price,
                    WarrantyMonths = p2.WarrantyMonths, RequiresShipping = true,
                    CreatedAt = order.CreatedAt,
                },
            };

            db.OrderItems.AddRange(items);
            await db.SaveChangesAsync();
            refs.OrderItems[$"order-{order.Id}-1"] = items[0].Id;
            refs.OrderItems[$"order-{order.Id}-2"] = items[1].Id;
            itemIndex++;
        }
        logger.LogInformation("Seeded order items");
    }

    private static async Task SeedOrderStatusHistoryAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.OrderStatusHistories.AnyAsync()) return;

        foreach (var order in refs.OrderEntities)
        {
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = null,
                ToStatus = "pending",
                ChangedByType = "system",
                CreatedAt = order.CreatedAt,
            });
            if (order.Status != "pending")
            {
                db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    FromStatus = "pending",
                    ToStatus = order.Status,
                    ChangedByType = "admin",
                    AdminUserId = refs.AdminUsers["admin@subul.iq"],
                    Note = $"Order moved to {order.Status}",
                    CreatedAt = order.CreatedAt.AddHours(2),
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded order status history");
    }

    private static async Task SeedFlashSaleProductsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.FlashSaleProducts.AnyAsync()) return;

        var products = new (string Slug, decimal SalePrice, decimal Original)[]
        {
            ("asus-tuf-gaming-f16-fx608jh", 1381250, 1625000),
            ("msi-cyborg-15-b13wfkg", 1487500, 1750000),
            ("logitech-g502-x-mouse", 80750, 95000),
            ("kingston-nv2-1tb-ssd", 55250, 65000),
            ("msi-g321q-monitor", 357000, 420000),
        };

        var sort = 0;
        foreach (var (slug, salePrice, original) in products)
        {
            db.FlashSaleProducts.Add(new FlashSaleProduct
            {
                FlashSaleId = refs.FlashSales["Summer Gaming Sale"],
                ProductId = refs.Products[slug],
                SalePrice = salePrice,
                OriginalPrice = original,
                QuantitySold = Random.Shared.Next(0, 5),
                SortOrder = sort++,
                CreatedAt = Now,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded flash sale products");
    }

    private static async Task SeedPurchaseOrderItemsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.PurchaseOrderItems.AnyAsync()) return;

        var po1Products = new[] { "asus-tuf-gaming-f16-fx608jh", "dell-pro-15-essential-pv15250", "hp-p24v-g5-monitor", "msi-g321q-monitor" };
        var po2Products = new[] { "asus-rog-strix-g16-g615jpr", "msi-cyborg-15-b13wfkg", "kingston-nv2-1tb-ssd", "logitech-mx-keys-keyboard" };

        foreach (var slug in po1Products)
        {
            var p = refs.ProductEntities.First(x => x.Slug == slug);
            db.PurchaseOrderItems.Add(new PurchaseOrderItem
            {
                PurchaseOrderId = refs.PurchaseOrders["PO-2026-00001"],
                ProductId = p.Id,
                Sku = p.Sku,
                ProductName = p.NameEn,
                QuantityOrdered = 5,
                QuantityReceived = 5,
                UnitCost = p.CostPrice ?? p.Price * 0.75m,
                TotalCost = (p.CostPrice ?? p.Price * 0.75m) * 5,
                CreatedAt = Now.AddDays(-14),
            });
        }

        foreach (var slug in po2Products)
        {
            var p = refs.ProductEntities.First(x => x.Slug == slug);
            db.PurchaseOrderItems.Add(new PurchaseOrderItem
            {
                PurchaseOrderId = refs.PurchaseOrders["PO-2026-00002"],
                ProductId = p.Id,
                Sku = p.Sku,
                ProductName = p.NameEn,
                QuantityOrdered = 10,
                QuantityReceived = 0,
                UnitCost = (p.CostPrice ?? p.Price * 0.75m) / 1310,
                TotalCost = (p.CostPrice ?? p.Price * 0.75m) / 1310 * 10,
                CreatedAt = Now.AddDays(-7),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded purchase order items");
    }

    private static async Task SeedPaymentTransactionsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.PaymentTransactions.AnyAsync()) return;

        foreach (var order in refs.OrderEntities)
        {
            var status = order.Status == "cancelled" ? "failed"
                : order.PaymentStatus == "paid" ? "completed" : "pending";

            db.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                PaymentMethodId = refs.PaymentMethods.GetValueOrDefault(order.PaymentMethod ?? "cod"),
                TransactionNumber = $"TXN-{order.Id:D6}",
                Amount = order.Total,
                Currency = "IQD",
                Status = status,
                Type = "charge",
                PaidAt = status == "completed" ? order.DeliveredAt ?? order.ShippedAt : null,
                CreatedAt = order.CreatedAt,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded payment transactions");
    }

    private static async Task SeedOrderDeliveriesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.OrderDeliveries.AnyAsync()) return;

        var agents = refs.DeliveryAgents.Values.ToArray();
        var deliveredOrShipped = refs.OrderEntities
            .Where(o => o.Status is "shipped" or "delivered")
            .ToList();

        for (var i = 0; i < deliveredOrShipped.Count; i++)
        {
            var order = deliveredOrShipped[i];
            db.OrderDeliveries.Add(new OrderDelivery
            {
                OrderId = order.Id,
                DeliveryAgentId = agents[i % agents.Length],
                AssignedAt = order.CreatedAt.AddDays(1),
                AssignedBy = refs.AdminUsers["ali@subul.iq"],
                Status = order.Status == "delivered" ? "delivered" : "in_transit",
                PickedUpAt = order.CreatedAt.AddDays(1.5),
                DeliveredAt = order.DeliveredAt,
                CreatedAt = order.CreatedAt.AddDays(1),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded order deliveries");
    }

    private static async Task SeedWishlistsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Wishlists.AnyAsync()) return;

        for (var i = 0; i < 10; i++)
        {
            db.Wishlists.Add(new Wishlist
            {
                UserId = refs.UserEntities[i].Id,
                ProductId = refs.ProductEntities[i % refs.ProductEntities.Count].Id,
                CreatedAt = Now.AddDays(-i),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded 10 wishlists");
    }

    private static async Task SeedBackInStockRequestsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.BackInStockRequests.AnyAsync()) return;

        var variantSkus = new[] { "ASUS-G615JPR-16-512", "MSI-B13WFKG-32-1TB", "HP-FD0000NE-GOLD", "KIN-NV2-2TB", "LOG-G502X-WHT" };
        for (var i = 0; i < variantSkus.Length; i++)
        {
            var variantId = refs.ProductVariants[variantSkus[i]];
            var variant = await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == variantId);
            db.BackInStockRequests.Add(new BackInStockRequest
            {
                UserId = refs.UserEntities[i].Id,
                Email = refs.UserEntities[i].Email,
                ProductId = variant.ProductId,
                VariantId = variantId,
                IsActive = true,
                CreatedAt = Now.AddDays(-i),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded back in stock requests");
    }

    private static async Task SeedInventoryMovementsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.InventoryMovements.AnyAsync()) return;

        foreach (var product in refs.ProductEntities)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                MovementType = "PURCHASE_IN",
                Quantity = product.StockQuantity,
                QuantityBefore = 0,
                QuantityAfter = product.StockQuantity,
                ReferenceType = "purchase_order",
                ReferenceId = refs.PurchaseOrders["PO-2026-00001"],
                UnitCost = product.CostPrice,
                Notes = "Initial stock seed",
                AdminUserId = refs.AdminUsers["admin@subul.iq"],
                CreatedAt = Now.AddDays(-10),
            });
        }

        var deliveredOrders = refs.OrderEntities.Where(o => o.Status == "delivered").Take(10);
        foreach (var order in deliveredOrders)
        {
            var items = await db.OrderItems.AsNoTracking().Where(oi => oi.OrderId == order.Id).ToListAsync();
            foreach (var item in items)
            {
                if (item.ProductId is null) continue;
                var product = refs.ProductEntities.First(p => p.Id == item.ProductId);
                db.InventoryMovements.Add(new InventoryMovement
                {
                    ProductId = item.ProductId.Value,
                    MovementType = "SALE_OUT",
                    Quantity = item.Quantity,
                    QuantityBefore = product.StockQuantity,
                    QuantityAfter = product.StockQuantity - item.Quantity,
                    ReferenceType = "order",
                    ReferenceId = order.Id,
                    AdminUserId = refs.AdminUsers["sara@subul.iq"],
                    CreatedAt = order.DeliveredAt ?? order.CreatedAt,
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded inventory movements");
    }

    // ── Tier 6 ──────────────────────────────────────────────────────────────

    private static async Task SeedReviewsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Reviews.AnyAsync()) return;

        var deliveredOrders = refs.OrderEntities.Where(o => o.Status == "delivered").Take(10).ToList();
        var titles = new (string En, string Ar, short Rating)[]
        {
            ("Excellent laptop!", "لابتوب ممتاز!", 5),
            ("Great value for money", "قيمة ممتازة مقابل السعر", 4),
            ("Fast delivery", "توصيل سريع", 5),
            ("Good product", "منتج جيد", 4),
            ("Works perfectly", "يعمل بشكل مثالي", 5),
            ("Recommended", "أنصح به", 4),
            ("Solid build quality", "جودة بناء قوية", 5),
            ("Happy with purchase", "راضٍ عن الشراء", 4),
            ("As described", "كما هو موصوف", 5),
            ("Would buy again", "سأشتري مرة أخرى", 4),
        };

        for (var i = 0; i < deliveredOrders.Count; i++)
        {
            var order = deliveredOrders[i];
            var orderItem = await db.OrderItems.AsNoTracking().FirstAsync(oi => oi.OrderId == order.Id);
            if (orderItem.ProductId is null || order.UserId is null) continue;

            db.Reviews.Add(new Review
            {
                ProductId = orderItem.ProductId.Value,
                UserId = order.UserId.Value,
                OrderItemId = orderItem.Id,
                Rating = titles[i].Rating,
                Title = titles[i].En,
                Body = $"{titles[i].En} — {titles[i].Ar}",
                Status = "approved",
                IsVerifiedPurchase = true,
                HelpfulCount = Random.Shared.Next(0, 15),
                CreatedAt = order.DeliveredAt ?? order.CreatedAt,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded reviews");
    }

    private static async Task SeedReturnsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Returns.AnyAsync())
        {
            var returns = await db.Returns.AsNoTracking().ToListAsync();
            foreach (var r in returns) refs.Returns[r.ReturnNumber] = r.Id;
            return;
        }

        var delivered = refs.OrderEntities.Where(o => o.Status == "delivered").Take(3).ToList();
        var statuses = new[] { "completed", "completed", "pending" };

        for (var i = 0; i < delivered.Count; i++)
        {
            var order = delivered[i];
            if (order.UserId is null) continue;

            var r = new Return
            {
                ReturnNumber = $"RET-2026-{1001 + i}",
                OrderId = order.Id,
                UserId = order.UserId.Value,
                ReturnType = "return",
                Status = statuses[i],
                Reason = "defective",
                ReasonDetails = "Product arrived with minor defect.",
                RefundAmount = order.Total * 0.5m,
                RefundMethod = "original_payment",
                RefundStatus = statuses[i] == "completed" ? "refunded" : "pending",
                ReviewedBy = statuses[i] != "pending" ? refs.AdminUsers["sara@subul.iq"] : null,
                ReviewedAt = statuses[i] != "pending" ? Now.AddDays(-1) : null,
                ReceivedAt = statuses[i] == "completed" ? Now.AddDays(-2) : null,
                CreatedAt = Now.AddDays(-5 + i),
            };
            db.Returns.Add(r);
            await db.SaveChangesAsync();
            refs.Returns[r.ReturnNumber] = r.Id;
        }
        logger.LogInformation("Seeded returns");
    }

    private static async Task SeedDiscountUsagesAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.DiscountUsages.AnyAsync()) return;

        var ordersWithDiscount = refs.OrderEntities.Where(o => o.DiscountId is not null).Take(5);
        foreach (var order in ordersWithDiscount)
        {
            db.DiscountUsages.Add(new DiscountUsage
            {
                DiscountId = refs.Discounts["WELCOME10"],
                OrderId = order.Id,
                UserId = order.UserId,
                AmountSaved = order.Subtotal * 0.1m,
                UsedAt = order.CreatedAt,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded discount usages");
    }

    private static async Task SeedCashCollectionsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.CashCollections.AnyAsync()) return;

        var agents = await db.DeliveryAgents.AsNoTracking().Take(3).ToListAsync();
        for (var i = 0; i < agents.Count; i++)
        {
            var expected = 500000m + i * 150000;
            db.CashCollections.Add(new CashCollection
            {
                DeliveryAgentId = agents[i].Id,
                CollectionDate = DateOnly.FromDateTime(Now.AddDays(-i)),
                ExpectedAmount = expected,
                CollectedAmount = expected,
                Difference = 0,
                Status = "received",
                ReceivedBy = refs.AdminUsers["admin@subul.iq"],
                ReceivedAt = Now.AddDays(-i),
                CreatedAt = Now.AddDays(-i),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded cash collections");
    }

    // ── Tier 7 ──────────────────────────────────────────────────────────────

    private static async Task SeedReturnItemsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ReturnItems.AnyAsync()) return;

        var returnNumbers = refs.Returns.Keys.ToArray();
        for (var i = 0; i < returnNumbers.Length; i++)
        {
            var returnId = refs.Returns[returnNumbers[i]];
            var ret = await db.Returns.AsNoTracking().FirstAsync(r => r.Id == returnId);
            var orderItems = await db.OrderItems.AsNoTracking().Where(oi => oi.OrderId == ret.OrderId).Take(2).ToListAsync();

            foreach (var oi in orderItems)
            {
                db.ReturnItems.Add(new ReturnItem
                {
                    ReturnId = returnId,
                    OrderItemId = oi.Id,
                    ProductId = oi.ProductId,
                    VariantId = oi.VariantId,
                    Quantity = 1,
                    UnitPrice = oi.UnitPrice,
                    RefundAmount = oi.TotalPrice * 0.5m,
                    Condition = i == 0 ? "defective" : "good",
                    ReturnToStock = i != 0,
                    CreatedAt = ret.CreatedAt,
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded return items");
    }

    private static async Task SeedWarrantyClaimsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.WarrantyClaims.AnyAsync()) return;

        var laptopOrders = refs.OrderEntities
            .Where(o => o.Status == "delivered")
            .Take(2)
            .ToList();

        var statuses = new[] { "open", "resolved" };
        for (var i = 0; i < laptopOrders.Count; i++)
        {
            var order = laptopOrders[i];
            if (order.UserId is null) continue;
            var orderItem = await db.OrderItems.AsNoTracking().FirstAsync(oi => oi.OrderId == order.Id);

            db.WarrantyClaims.Add(new WarrantyClaim
            {
                ClaimNumber = $"WC-2026-{1001 + i}",
                OrderItemId = orderItem.Id,
                ProductId = orderItem.ProductId,
                UserId = order.UserId.Value,
                IssueDescription = "Screen flickering issue after 2 weeks of use.",
                Status = statuses[i],
                Resolution = statuses[i] == "resolved" ? "repair" : null,
                WarrantyExpiresAt = DateOnly.FromDateTime(Now.AddMonths(10)),
                ReceivedAt = Now.AddDays(-3),
                ResolvedAt = statuses[i] == "resolved" ? Now.AddDays(-1) : null,
                HandledBy = refs.AdminUsers["ali@subul.iq"],
                CreatedAt = Now.AddDays(-4),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded warranty claims");
    }

    private static async Task SeedNotificationsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.Notifications.AnyAsync()) return;

        for (var i = 0; i < 10; i++)
        {
            var user = refs.UserEntities[i];
            var order = refs.OrderEntities[i];
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = "order_status",
                Title = "Order Update",
                Body = $"Your order {order.OrderNumber} status: {order.Status}",
                Data = $"{{\"orderId\":{order.Id},\"status\":\"{order.Status}\"}}",
                ReadAt = i % 2 == 0 ? Now.AddDays(-1) : null,
                CreatedAt = Now.AddDays(-i),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded notifications");
    }

    private static async Task SeedActivityLogsAsync(AppDbContext db, SeedReferences refs, ILogger logger)
    {
        if (await db.ActivityLogs.AnyAsync()) return;

        var actions = new (string Action, string EntityType, long? EntityId)[]
        {
            ("create", "product", refs.Products.GetValueOrDefault("asus-tuf-gaming-f16-fx608jh")),
            ("update", "category", refs.Categories.GetValueOrDefault("laptops")),
            ("update", "order", refs.OrderEntities.FirstOrDefault()?.Id),
            ("create", "brand", refs.Brands.GetValueOrDefault("asus")),
            ("update", "product", refs.Products.GetValueOrDefault("msi-g321q-monitor")),
            ("change_status", "order", refs.OrderEntities.Skip(1).FirstOrDefault()?.Id),
            ("create", "collection", refs.Collections.GetValueOrDefault("best-sellers")),
            ("update", "shipping_zone", refs.ShippingZones.GetValueOrDefault("baghdad")),
            ("create", "discount", refs.Discounts.GetValueOrDefault("WELCOME10")),
            ("update", "payment_method", refs.PaymentMethods.GetValueOrDefault("cod")),
            ("create", "banner", 1),
            ("update", "product", refs.Products.GetValueOrDefault("logitech-g502-x-mouse")),
            ("change_status", "order", refs.OrderEntities.Skip(2).FirstOrDefault()?.Id),
            ("create", "flash_sale", refs.FlashSales.GetValueOrDefault("Summer Gaming Sale")),
            ("update", "category", refs.Categories.GetValueOrDefault("monitors")),
            ("create", "supplier", refs.Suppliers.GetValueOrDefault("Baghdad Electronics Supplier")),
            ("update", "order", refs.OrderEntities.Skip(3).FirstOrDefault()?.Id),
            ("create", "page", refs.Pages.GetValueOrDefault("about-us")),
            ("update", "product", refs.Products.GetValueOrDefault("kingston-nv2-1tb-ssd")),
            ("login", "admin_user", refs.AdminUsers.GetValueOrDefault("admin@subul.iq")),
        };

        for (var i = 0; i < actions.Length; i++)
        {
            var (action, entityType, entityId) = actions[i];
            db.ActivityLogs.Add(new ActivityLog
            {
                AdminUserId = refs.AdminUsers["admin@subul.iq"],
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = "127.0.0.1",
                CreatedAt = Now.AddHours(-i),
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded activity logs");
    }
}
