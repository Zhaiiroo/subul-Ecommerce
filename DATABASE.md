# DATABASE.md

PostgreSQL schema for Subul-Ecommerce. **Read one table section per task** — never load the full file into context unless doing schema-wide work.

Source of truth: `backend/Domain/Entities/*.cs` + `backend/Infrastructure/Persistence/AppDbContext.Partial.cs`. Timestamps use `DateTime.Now` (Npgsql legacy timestamp behavior).

## Quick index

| Table | Entity | Admin CRUD priority |
|-------|--------|---------------------|
| `categories` | Category | high |
| `products` | Product | high |
| `orders` | Order | high |
| `carts` | Cart | high |
| `users` | User | high |
| `brands` | Brand | high |
| `activity_logs` | ActivityLog | — |
| `addresses` | Address | — |
| `admin_users` | AdminUser | — |
| `attribute_groups` | AttributeGroup | — |
| `attributes` | Attribute | — |
| `back_in_stock_requests` | BackInStockRequest | — |
| `banners` | Banner | — |
| `cart_items` | CartItem | — |
| `cash_collections` | CashCollection | — |
| `collection_products` | CollectionProduct | — |
| `collections` | Collection | — |
| `contact_messages` | ContactMessage | — |
| `delivery_agents` | DeliveryAgent | — |
| `discount_conditions` | DiscountCondition | — |
| `discount_usages` | DiscountUsage | — |
| `discounts` | Discount | — |
| `flash_sale_products` | FlashSaleProduct | — |
| `flash_sales` | FlashSale | — |
| `inventory_movements` | InventoryMovement | — |
| `menu_items` | MenuItem | — |
| `menus` | Menu | — |
| `notifications` | Notification | — |
| `order_deliveries` | OrderDelivery | — |
| `order_items` | OrderItem | — |
| `order_status_history` | OrderStatusHistory | — |
| `pages` | Page | — |
| `payment_methods` | PaymentMethod | — |
| `payment_transactions` | PaymentTransaction | — |
| `product_attribute_values` | ProductAttributeValue | — |
| `product_compares` | ProductCompare | — |
| `product_images` | ProductImage | — |
| `product_tags` | ProductTag | — |
| `product_variants` | ProductVariant | — |
| `purchase_order_items` | PurchaseOrderItem | — |
| `purchase_orders` | PurchaseOrder | — |
| `return_items` | ReturnItem | — |
| `returns` | Return | — |
| `reviews` | Review | — |
| `settings` | Setting | — |
| `shipping_rates` | ShippingRate | — |
| `shipping_zones` | ShippingZone | — |
| `suppliers` | Supplier | — |
| `tags` | Tag | — |
| `warranty_claims` | WarrantyClaim | — |
| `wishlists` | Wishlist | — |

## Entity notes (non-obvious)

| Entity | Notes |
|--------|-------|
| **Product** | `currency` (string, required), `status` (string — not `IsActive`/`IsDigital`). Price/stock on product row. |
| **Order** | `status`, `payment_status`, `fulfillment_status` are separate strings. See `order_status_history` for audit trail. |
| **Cart** | Guest carts use `session_id`; `user_id` nullable. Keep `api/carts/*` anonymous per security rules. |
| **Attribute** | EF DbSet alias `AttributeEntity` — C# keyword collision. Table: `attributes`. |
| **Category** | Self-FK `parent_id`. Delete guards in `DeleteCategoryHandler` (children + products). |

---

## `activity_logs`

Entity: `backend/Domain/Entities/ActivityLog.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `admin_user_id` | long | yes | FK → `admin_users` |
| `action` | string | no |  |
| `entity_type` | string | yes |  |
| `entity_id` | long | yes | FK → `entity` |
| `old_values` | string | yes |  |
| `new_values` | string | yes |  |
| `ip_address` | string | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `AdminUser`

---

## `addresses`

Entity: `backend/Domain/Entities/Address.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | no | FK → `users` |
| `first_name` | string | yes |  |
| `last_name` | string | yes |  |
| `phone` | string | yes |  |
| `address1` | string | no |  |
| `address2` | string | yes |  |
| `city` | string | yes |  |
| `governorate` | string | yes |  |
| `country` | string | no |  |
| `is_default` | bool | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `User`

---

## `admin_users`

Entity: `backend/Domain/Entities/AdminUser.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `email` | string | no |  |
| `password_hash` | string | no |  |
| `role` | string | no |  |
| `is_active` | bool | no |  |
| `must_change_password` | bool | no | default `false`; while true only `api/auth/me`, `api/auth/change-password` and `api/auth/logout` are reachable |
| `password_changed_at` | DateTime | yes | session stamp — a token minted before this value is rejected |
| `last_login_at` | DateTime | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `ActivityLogs`, `CashCollections`, `ContactMessages`, `FlashSales`, `InventoryMovements`, `OrderDeliveries`, `OrderStatusHistories`, `PurchaseOrders`, `Returns`, `WarrantyClaims`

`must_change_password` and `password_changed_at` were added after the Initial migration —
DDL in `docs/sql/2026-09-10-admin-user-password-policy.sql`, mapping in `AppDbContext.Partial.cs`.

---

## `attribute_groups`

Entity: `backend/Domain/Entities/AttributeGroup.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `slug` | string | yes |  |
| `sort_order` | int | no |  |
| `is_filterable` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Attributes`

---

## `attributes`

Entity: `backend/Domain/Entities/Attribute.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `group_id` | long | yes | FK → `group` |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `slug` | string | yes |  |
| `unit` | string | yes |  |
| `input_type` | string | no |  |
| `is_filterable` | bool | no |  |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Group`, `ProductAttributeValues`

---

## `back_in_stock_requests`

Entity: `backend/Domain/Entities/BackInStockRequest.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | yes | FK → `users` |
| `email` | string | yes |  |
| `product_id` | long | no | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `notified_at` | DateTime | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `User`, `Variant`

---

## `banners`

Entity: `backend/Domain/Entities/Banner.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `title_en` | string | yes |  |
| `title_ar` | string | yes |  |
| `subtitle_en` | string | yes |  |
| `subtitle_ar` | string | yes |  |
| `image_url` | string | no |  |
| `mobile_image_url` | string | yes |  |
| `link_url` | string | yes |  |
| `button_text_en` | string | yes |  |
| `button_text_ar` | string | yes |  |
| `position` | string | yes |  |
| `sort_order` | int | no |  |
| `starts_at` | DateTime | yes |  |
| `ends_at` | DateTime | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

---

## `brands`

Entity: `backend/Domain/Entities/Brand.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `slug` | string | no |  |
| `logo_url` | string | yes |  |
| `banner_url` | string | yes |  |
| `description_en` | string | yes |  |
| `description_ar` | string | yes |  |
| `website_url` | string | yes |  |
| `is_featured` | bool | no |  |
| `is_active` | bool | no |  |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `Products`

---

## `cart_items`

Entity: `backend/Domain/Entities/CartItem.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `cart_id` | long | no | FK → `carts` |
| `product_id` | long | no | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `quantity` | int | no |  |
| `unit_price` | decimal | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `Cart`, `Product`, `Variant`

---

## `carts`

Entity: `backend/Domain/Entities/Cart.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | yes | FK → `users` |
| `session_id` | string | yes | FK → `session` |
| `coupon_code` | string | yes |  |
| `notes` | string | yes |  |
| `expires_at` | DateTime | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `CartItems`, `User`

---

## `cash_collections`

Entity: `backend/Domain/Entities/CashCollection.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `delivery_agent_id` | long | no | FK → `delivery_agents` |
| `collection_date` | DateOnly | no |  |
| `expected_amount` | decimal | yes |  |
| `collected_amount` | decimal | yes |  |
| `difference` | decimal | yes |  |
| `status` | string | no |  |
| `received_by` | long | yes |  |
| `received_at` | DateTime | yes |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `DeliveryAgent`, `ReceivedByNavigation`

---

## `categories`

Entity: `backend/Domain/Entities/Category.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `parent_id` | long | yes | FK → `parent` |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `slug` | string | no |  |
| `description_en` | string | yes |  |
| `description_ar` | string | yes |  |
| `image_url` | string | yes |  |
| `banner_url` | string | yes |  |
| `sort_order` | int | no |  |
| `is_active` | bool | no |  |
| `seo_title` | string | yes |  |
| `seo_description` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `InverseParent`, `Parent`, `Products`

---

## `collection_products`

Entity: `backend/Domain/Entities/CollectionProduct.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `collection_id` | long | no | FK → `collections` |
| `product_id` | long | no | FK → `products` |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Collection`, `Product`

---

## `collections`

Entity: `backend/Domain/Entities/Collection.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `slug` | string | no |  |
| `description_en` | string | yes |  |
| `description_ar` | string | yes |  |
| `image_url` | string | yes |  |
| `banner_url` | string | yes |  |
| `collection_type` | string | no |  |
| `is_active` | bool | no |  |
| `sort_order` | int | no |  |
| `meta_title` | string | yes |  |
| `meta_description` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `CollectionProducts`

---

## `contact_messages`

Entity: `backend/Domain/Entities/ContactMessage.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | yes | FK → `users` |
| `name` | string | no |  |
| `email` | string | no |  |
| `phone` | string | yes |  |
| `subject` | string | yes |  |
| `message` | string | no |  |
| `status` | string | no |  |
| `replied_at` | DateTime | yes |  |
| `replied_by` | long | yes |  |
| `reply_message` | string | yes |  |
| `ip_address` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `RepliedByNavigation`, `User`

---

## `delivery_agents`

Entity: `backend/Domain/Entities/DeliveryAgent.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `phone` | string | yes |  |
| `whatsapp` | string | yes |  |
| `national_id` | string | yes | FK → `national` |
| `area` | string | yes |  |
| `is_active` | bool | no |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `CashCollections`, `OrderDeliveries`, `PaymentTransactions`

---

## `discount_conditions`

Entity: `backend/Domain/Entities/DiscountCondition.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `discount_id` | long | no | FK → `discounts` |
| `entity_type` | string | yes |  |
| `entity_id` | long | yes | FK → `entity` |
| `created_at` | DateTime | no |  |

**Relations:** `Discount`

---

## `discount_usages`

Entity: `backend/Domain/Entities/DiscountUsage.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `discount_id` | long | no | FK → `discounts` |
| `order_id` | long | no | FK → `orders` |
| `user_id` | long | yes | FK → `users` |
| `amount_saved` | decimal | yes |  |
| `used_at` | DateTime | no |  |

**Relations:** `Discount`, `Order`, `User`

---

## `discounts`

Entity: `backend/Domain/Entities/Discount.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `code` | string | yes |  |
| `name` | string | yes |  |
| `type` | string | yes |  |
| `value` | decimal | yes |  |
| `currency` | string | no |  |
| `min_order_value` | decimal | yes |  |
| `min_quantity` | int | yes |  |
| `usage_limit` | int | yes |  |
| `usage_count` | int | no |  |
| `per_customer_limit` | int | no |  |
| `applies_to` | string | yes |  |
| `starts_at` | DateTime | yes |  |
| `ends_at` | DateTime | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `DiscountConditions`, `DiscountUsages`, `Orders`

---

## `flash_sale_products`

Entity: `backend/Domain/Entities/FlashSaleProduct.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `flash_sale_id` | long | no | FK → `flash_sales` |
| `product_id` | long | no | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `sale_price` | decimal | no |  |
| `original_price` | decimal | yes |  |
| `max_quantity_per_order` | int | yes |  |
| `quantity_limit` | int | yes |  |
| `quantity_sold` | int | no |  |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `FlashSale`, `Product`, `Variant`

---

## `flash_sales`

Entity: `backend/Domain/Entities/FlashSale.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `description_en` | string | yes |  |
| `description_ar` | string | yes |  |
| `banner_url` | string | yes |  |
| `discount_type` | string | yes |  |
| `discount_value` | decimal | yes |  |
| `starts_at` | DateTime | no |  |
| `ends_at` | DateTime | no |  |
| `is_active` | bool | no |  |
| `max_uses` | int | yes |  |
| `uses_count` | int | no |  |
| `sort_order` | int | no |  |
| `created_by` | long | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `CreatedByNavigation`, `FlashSaleProducts`

---

## `inventory_movements`

Entity: `backend/Domain/Entities/InventoryMovement.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `movement_type` | string | no |  |
| `quantity` | int | no |  |
| `quantity_before` | int | no |  |
| `quantity_after` | int | no |  |
| `reference_type` | string | yes |  |
| `reference_id` | long | yes | FK → `reference` |
| `unit_cost` | decimal | yes |  |
| `notes` | string | yes |  |
| `admin_user_id` | long | yes | FK → `admin_users` |
| `created_at` | DateTime | no |  |

**Relations:** `AdminUser`, `Product`, `Variant`

---

## `menu_items`

Entity: `backend/Domain/Entities/MenuItem.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `menu_id` | long | no | FK → `menus` |
| `parent_id` | long | yes | FK → `parent` |
| `label_en` | string | yes |  |
| `label_ar` | string | yes |  |
| `url` | string | yes |  |
| `link_type` | string | yes |  |
| `link_id` | long | yes | FK → `link` |
| `icon` | string | yes |  |
| `target` | string | no |  |
| `sort_order` | int | no |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `InverseParent`, `Menu`, `Parent`

---

## `menus`

Entity: `backend/Domain/Entities/Menu.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `label_en` | string | yes |  |
| `label_ar` | string | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `MenuItems`

---

## `notifications`

Entity: `backend/Domain/Entities/Notification.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | no | FK → `users` |
| `type` | string | no |  |
| `title` | string | yes |  |
| `body` | string | yes |  |
| `data` | string | yes |  |
| `read_at` | DateTime | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `User`

---

## `order_deliveries`

Entity: `backend/Domain/Entities/OrderDelivery.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `order_id` | long | no | FK → `orders` |
| `delivery_agent_id` | long | no | FK → `delivery_agents` |
| `assigned_at` | DateTime | yes |  |
| `assigned_by` | long | yes |  |
| `status` | string | no |  |
| `picked_up_at` | DateTime | yes |  |
| `attempted_at` | DateTime | yes |  |
| `delivered_at` | DateTime | yes |  |
| `failure_reason` | string | yes |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `AssignedByNavigation`, `DeliveryAgent`, `Order`

---

## `order_items`

Entity: `backend/Domain/Entities/OrderItem.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `order_id` | long | no | FK → `orders` |
| `product_id` | long | yes | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `product_name` | string | no |  |
| `sku` | string | yes |  |
| `quantity` | int | no |  |
| `unit_price` | decimal | no |  |
| `compare_at_price` | decimal | yes |  |
| `discount_amount` | decimal | no |  |
| `total_price` | decimal | no |  |
| `warranty_months` | int | no |  |
| `requires_shipping` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Order`, `Product`, `ReturnItems`, `Reviews`, `Variant`, `WarrantyClaims`

---

## `order_status_history`

Entity: `backend/Domain/Entities/OrderStatusHistory.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `order_id` | long | no | FK → `orders` |
| `from_status` | string | yes |  |
| `to_status` | string | no |  |
| `changed_by_type` | string | yes |  |
| `admin_user_id` | long | yes | FK → `admin_users` |
| `note` | string | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `AdminUser`, `Order`

---

## `orders`

Entity: `backend/Domain/Entities/Order.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | yes | FK → `users` |
| `order_number` | string | no |  |
| `status` | string | no |  |
| `payment_status` | string | no |  |
| `fulfillment_status` | string | no |  |
| `subtotal` | decimal | no |  |
| `discount_amount` | decimal | no |  |
| `shipping_amount` | decimal | no |  |
| `tax_amount` | decimal | no |  |
| `total` | decimal | no |  |
| `currency` | string | no |  |
| `discount_id` | long | yes | FK → `discounts` |
| `coupon_code` | string | yes |  |
| `shipping_first_name` | string | yes |  |
| `shipping_last_name` | string | yes |  |
| `shipping_phone` | string | yes |  |
| `shipping_address1` | string | yes |  |
| `shipping_address2` | string | yes |  |
| `shipping_city` | string | yes |  |
| `shipping_governorate` | string | yes |  |
| `shipping_country` | string | no |  |
| `shipping_zone_id` | long | yes | FK → `shipping_zones` |
| `payment_method` | string | yes |  |
| `tracking_number` | string | yes |  |
| `notes` | string | yes |  |
| `customer_notes` | string | yes |  |
| `cancelled_at` | DateTime | yes |  |
| `cancel_reason` | string | yes |  |
| `shipped_at` | DateTime | yes |  |
| `delivered_at` | DateTime | yes |  |
| `ip_address` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `Discount`, `DiscountUsages`, `OrderDeliveries`, `OrderItems`, `OrderStatusHistories`, `PaymentTransactions`, `Returns`, `ShippingZone`, `User`

---

## `pages`

Entity: `backend/Domain/Entities/Page.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `title_en` | string | no |  |
| `title_ar` | string | yes |  |
| `slug` | string | no |  |
| `content_en` | string | yes |  |
| `content_ar` | string | yes |  |
| `is_published` | bool | no |  |
| `meta_title` | string | yes |  |
| `meta_description` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

---

## `payment_methods`

Entity: `backend/Domain/Entities/PaymentMethod.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `label_en` | string | yes |  |
| `label_ar` | string | yes |  |
| `type` | string | yes |  |
| `gateway` | string | yes |  |
| `gateway_config` | string | yes |  |
| `icon_url` | string | yes |  |
| `instructions_en` | string | yes |  |
| `instructions_ar` | string | yes |  |
| `is_active` | bool | no |  |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `PaymentTransactions`

---

## `payment_transactions`

Entity: `backend/Domain/Entities/PaymentTransaction.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `order_id` | long | no | FK → `orders` |
| `payment_method_id` | long | yes | FK → `payment_methods` |
| `transaction_number` | string | yes |  |
| `amount` | decimal | no |  |
| `currency` | string | no |  |
| `status` | string | no |  |
| `type` | string | no |  |
| `collected_by` | long | yes |  |
| `collected_at` | DateTime | yes |  |
| `gateway_name` | string | yes |  |
| `gateway_transaction_id` | string | yes | FK → `gatewaytransaction` |
| `gateway_status` | string | yes |  |
| `gateway_response` | string | yes |  |
| `failure_reason` | string | yes |  |
| `paid_at` | DateTime | yes |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `CollectedByNavigation`, `Order`, `PaymentMethod`

---

## `product_attribute_values`

Entity: `backend/Domain/Entities/ProductAttributeValue.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `attribute_id` | long | no | FK → `attributes` |
| `value_text` | string | yes |  |
| `value_number` | decimal | yes |  |
| `value_boolean` | bool | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `Attribute`, `Product`

---

## `product_compares`

Entity: `backend/Domain/Entities/ProductCompare.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `session_id` | string | yes | FK → `session` |
| `user_id` | long | yes | FK → `users` |
| `product_id` | long | no | FK → `products` |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `User`

---

## `product_images`

Entity: `backend/Domain/Entities/ProductImage.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `image_url` | string | no |  |
| `alt_text` | string | yes |  |
| `sort_order` | int | no |  |
| `is_primary` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `Variant`

---

## `product_tags`

Entity: `backend/Domain/Entities/ProductTag.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `tag_id` | long | no | FK → `tags` |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `Tag`

---

## `product_variants`

Entity: `backend/Domain/Entities/ProductVariant.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `title` | string | yes |  |
| `sku` | string | yes |  |
| `barcode` | string | yes |  |
| `price` | decimal | yes |  |
| `compare_at_price` | decimal | yes |  |
| `cost_price` | decimal | yes |  |
| `stock_quantity` | int | no |  |
| `weight` | decimal | yes |  |
| `is_active` | bool | no |  |
| `sort_order` | int | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `BackInStockRequests`, `CartItems`, `FlashSaleProducts`, `InventoryMovements`, `OrderItems`, `Product`, `ProductImages`, `PurchaseOrderItems`, `ReturnItems`

---

## `products`

Entity: `backend/Domain/Entities/Product.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `category_id` | long | yes | FK → `categories` |
| `brand_id` | long | yes | FK → `brands` |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `slug` | string | no |  |
| `sku` | string | yes |  |
| `barcode` | string | yes |  |
| `description_en` | string | yes |  |
| `description_ar` | string | yes |  |
| `short_description_en` | string | yes |  |
| `short_description_ar` | string | yes |  |
| `price` | decimal | no |  |
| `compare_at_price` | decimal | yes |  |
| `cost_price` | decimal | yes |  |
| `currency` | string | no |  |
| `stock_quantity` | int | no |  |
| `low_stock_threshold` | int | no |  |
| `min_order_quantity` | int | no |  |
| `weight` | decimal | yes |  |
| `status` | string | no |  |
| `is_featured` | bool | no |  |
| `requires_shipping` | bool | no |  |
| `warranty_months` | int | no |  |
| `warranty_description` | string | yes |  |
| `total_sold` | int | no |  |
| `views_count` | int | no |  |
| `meta_title` | string | yes |  |
| `meta_description` | string | yes |  |
| `tags` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `BackInStockRequests`, `Brand`, `CartItems`, `Category`, `CollectionProducts`, `FlashSaleProducts`, `InventoryMovements`, `OrderItems`, `ProductAttributeValues`, `ProductCompares`, `ProductImages`, `ProductTags`, `ProductVariants`, `PurchaseOrderItems`, `ReturnItems`, `Reviews`, `WarrantyClaims`, `Wishlists`

---

## `purchase_order_items`

Entity: `backend/Domain/Entities/PurchaseOrderItem.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `purchase_order_id` | long | no | FK → `purchase_orders` |
| `product_id` | long | yes | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `sku` | string | yes |  |
| `product_name` | string | yes |  |
| `quantity_ordered` | int | no |  |
| `quantity_received` | int | no |  |
| `unit_cost` | decimal | no |  |
| `total_cost` | decimal | yes |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `PurchaseOrder`, `Variant`

---

## `purchase_orders`

Entity: `backend/Domain/Entities/PurchaseOrder.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `supplier_id` | long | no | FK → `suppliers` |
| `po_number` | string | no |  |
| `status` | string | no |  |
| `order_date` | DateOnly | yes |  |
| `expected_date` | DateOnly | yes |  |
| `received_date` | DateOnly | yes |  |
| `subtotal` | decimal | yes |  |
| `tax_amount` | decimal | no |  |
| `shipping_cost` | decimal | no |  |
| `total` | decimal | yes |  |
| `currency` | string | no |  |
| `exchange_rate` | decimal | no |  |
| `payment_status` | string | no |  |
| `paid_amount` | decimal | no |  |
| `notes` | string | yes |  |
| `admin_user_id` | long | yes | FK → `admin_users` |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `AdminUser`, `PurchaseOrderItems`, `Supplier`

---

## `return_items`

Entity: `backend/Domain/Entities/ReturnItem.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `return_id` | long | no | FK → `returns` |
| `order_item_id` | long | no | FK → `order_items` |
| `product_id` | long | yes | FK → `products` |
| `variant_id` | long | yes | FK → `variant` |
| `quantity` | int | no |  |
| `unit_price` | decimal | yes |  |
| `refund_amount` | decimal | yes |  |
| `condition` | string | yes |  |
| `return_to_stock` | bool | no |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |

**Relations:** `OrderItem`, `Product`, `Return`, `Variant`

---

## `returns`

Entity: `backend/Domain/Entities/Return.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `return_number` | string | no |  |
| `order_id` | long | no | FK → `orders` |
| `user_id` | long | no | FK → `users` |
| `return_type` | string | yes |  |
| `status` | string | no |  |
| `reason` | string | yes |  |
| `reason_details` | string | yes |  |
| `refund_amount` | decimal | yes |  |
| `refund_method` | string | yes |  |
| `refund_status` | string | no |  |
| `admin_notes` | string | yes |  |
| `reviewed_by` | long | yes |  |
| `reviewed_at` | DateTime | yes |  |
| `received_at` | DateTime | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `Order`, `ReturnItems`, `ReviewedByNavigation`, `User`, `WarrantyClaims`

---

## `reviews`

Entity: `backend/Domain/Entities/Review.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `product_id` | long | no | FK → `products` |
| `user_id` | long | no | FK → `users` |
| `order_item_id` | long | yes | FK → `order_items` |
| `rating` | short | no |  |
| `title` | string | yes |  |
| `body` | string | yes |  |
| `status` | string | no |  |
| `is_verified_purchase` | bool | no |  |
| `helpful_count` | int | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `OrderItem`, `Product`, `User`

---

## `settings`

Entity: `backend/Domain/Entities/Setting.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `key` | string | no |  |
| `value` | string | yes |  |
| `type` | string | no |  |
| `group` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

---

## `shipping_rates`

Entity: `backend/Domain/Entities/ShippingRate.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `shipping_zone_id` | long | no | FK → `shipping_zones` |
| `name_en` | string | yes |  |
| `name_ar` | string | yes |  |
| `rate_type` | string | no |  |
| `price` | decimal | no |  |
| `min_order_value` | decimal | yes |  |
| `max_order_value` | decimal | yes |  |
| `free_shipping_threshold` | decimal | yes |  |
| `estimated_days_min` | int | yes |  |
| `estimated_days_max` | int | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `ShippingZone`

---

## `shipping_zones`

Entity: `backend/Domain/Entities/ShippingZone.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name_en` | string | no |  |
| `name_ar` | string | yes |  |
| `governorates` | string | yes | JSON array of strings, e.g. `["Baghdad"]` |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `Orders`, `ShippingRates`

---

## `suppliers`

Entity: `backend/Domain/Entities/Supplier.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `company_name` | string | yes |  |
| `email` | string | yes |  |
| `phone` | string | yes |  |
| `whatsapp` | string | yes |  |
| `country` | string | yes |  |
| `city` | string | yes |  |
| `address` | string | yes |  |
| `payment_terms` | string | yes |  |
| `currency` | string | no |  |
| `notes` | string | yes |  |
| `is_active` | bool | no |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `PurchaseOrders`

---

## `tags`

Entity: `backend/Domain/Entities/Tag.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `name` | string | no |  |
| `slug` | string | no |  |
| `created_at` | DateTime | no |  |

**Relations:** `ProductTags`

---

## `users`

Entity: `backend/Domain/Entities/User.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `email` | string | no |  |
| `password_hash` | string | yes |  |
| `first_name` | string | yes |  |
| `last_name` | string | yes |  |
| `phone` | string | yes |  |
| `accepts_marketing` | bool | no |  |
| `is_active` | bool | no |  |
| `email_verified_at` | DateTime | yes |  |
| `store_credit` | decimal | no |  |
| `notes` | string | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `Addresses`, `BackInStockRequests`, `Carts`, `ContactMessages`, `DiscountUsages`, `Notifications`, `Orders`, `ProductCompares`, `Returns`, `Reviews`, `WarrantyClaims`, `Wishlists`

---

## `warranty_claims`

Entity: `backend/Domain/Entities/WarrantyClaim.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `claim_number` | string | no |  |
| `order_item_id` | long | no | FK → `order_items` |
| `product_id` | long | yes | FK → `products` |
| `user_id` | long | no | FK → `users` |
| `return_id` | long | yes | FK → `returns` |
| `issue_description` | string | no |  |
| `status` | string | no |  |
| `resolution` | string | yes |  |
| `warranty_expires_at` | DateOnly | yes |  |
| `received_at` | DateTime | yes |  |
| `resolved_at` | DateTime | yes |  |
| `admin_notes` | string | yes |  |
| `handled_by` | long | yes |  |
| `created_at` | DateTime | no |  |
| `updated_at` | DateTime | yes |  |

**Relations:** `HandledByNavigation`, `OrderItem`, `Product`, `Return`, `User`

---

## `wishlists`

Entity: `backend/Domain/Entities/Wishlist.cs`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `id` | long | no |  |
| `user_id` | long | no | FK → `users` |
| `product_id` | long | no | FK → `products` |
| `created_at` | DateTime | no |  |

**Relations:** `Product`, `User`