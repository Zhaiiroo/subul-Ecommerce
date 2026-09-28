using System;
using System.Collections.Generic;

namespace backend.Domain.Entities;

public partial class AdminUser
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    /// <summary>
    /// Set on every account the panel or the bootstrapper creates, and again on
    /// every admin-issued reset. While it is true the session may only reach the
    /// change-password route.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Doubles as the session stamp: <see cref="Common.Auth.JwtTokenService"/>
    /// writes it into the token and <see cref="Common.Auth.AdminSessionValidator"/>
    /// rejects tokens minted before the current value, so a reset logs the
    /// account out of every device it was signed in on.
    /// </summary>
    public DateTime? PasswordChangedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<CashCollection> CashCollections { get; set; } = new List<CashCollection>();

    public virtual ICollection<ContactMessage> ContactMessages { get; set; } = new List<ContactMessage>();

    public virtual ICollection<FlashSale> FlashSales { get; set; } = new List<FlashSale>();

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<OrderDelivery> OrderDeliveries { get; set; } = new List<OrderDelivery>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();
}
