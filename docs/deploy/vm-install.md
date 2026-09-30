# دليل رفع سُبل على الـ VM

الدليل خطوة بخطوة لتثبيت النظام أول مرة على **Ubuntu 24.04 LTS (x86_64)**، ثم تحديثه، والنسخ الاحتياطي، وربط الدومين لاحقاً.
الأوامر تُنسخ كما هي. ما بين `<...>` تستبدله بقيمتك.

## الصورة العامة

```
حاسوبك (Windows)                          الـ VM (Ubuntu)
─────────────────                         ────────────────────────────────────────
release-export.sh ──► subul-release-<tag>.tar ──scp/FileZilla──► /opt/subul/incoming/
                                                               │
                                          release-import.sh ◄──┘
                                                               ▼
                                          /opt/subul/releases/subul-release-<tag>/
                                          /opt/subul/current ──► (الإصدار الفعّال)
                                          /usr/local/bin/subul ──► current/subul

                                          /etc/subul/production.env   الأسرار (خارج أي إصدار)
                                          /srv/subul/backups/database النسخ الاحتياطية
                                          /srv/subul/img              الصور المرفوعة
                                          Docker volume: subul-production-postgres
```

- **الكود لا يُبنى على السيرفر أبداً.** يُبنى على حاسوبك بعد أن تنجح الاختبارات وفحص الثغرات، ثم يُنقل كأرشيف واحد.
- **البيانات خارج الإصدار:** قاعدة البيانات على volume خارجي لا يحذفه `down -v`، والصور والنسخ الاحتياطية في `/srv/subul`، والأسرار في `/etc/subul`. تبديل الإصدار أو الرجوع إلى سابق لا يلمس أياً منها.
- **المنافذ المفتوحة للعالم: 80 و443 فقط.** قاعدة البيانات وRedis والتطبيقات بلا أي منفذ على السيرفر.

---

## 1. تجهيز السيرفر

ادخل بالحساب الذي أعطاك إياه مزوّد الخدمة، ثم:

```bash
sudo apt update && sudo apt -y full-upgrade
sudo timedatectl set-timezone Asia/Baghdad
sudo apt -y install unattended-upgrades ufw
sudo dpkg-reconfigure -plow unattended-upgrades
```

**مستخدم للإدارة** (إن كنت تدخل بـ `root`):

```bash
sudo adduser <username>
sudo usermod -aG sudo <username>
```

**مفتاح SSH بدل كلمة المرور.** على حاسوبك (PowerShell):

```powershell
ssh-keygen -t ed25519
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh <username>@<IP> "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"
```

تأكد أنك تدخل بالمفتاح **في نافذة جديدة** قبل الخطوة التالية، وأبقِ النافذة الحالية مفتوحة. ثم أغلق الدخول بكلمة مرور وبحساب root:

```bash
sudo tee /etc/ssh/sshd_config.d/10-hardening.conf >/dev/null <<'EOF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
EOF
sudo systemctl reload ssh
```

**الجدار الناري:**

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

> ⚠️ **Docker يتجاوز ufw** في المنافذ التي ينشرها. لذلك الضمان الحقيقي أن `compose.production.yaml` لا ينشر إلا 80 و443. وإن كانت لوحة مزوّد الـ VM توفر جداراً نارياً، افتح فيه 22 و80 و443 فقط: هذا الجدار يعمل قبل Docker. وإن كان عنوانك ثابتاً فاقصر 22 عليه.

## 2. تثبيت Docker

من مستودع Docker الرسمي، لا من حزمة Ubuntu القديمة:

```bash
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
sudo apt update
sudo apt -y install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo docker run --rm hello-world
```

لا تضف مستخدمك إلى مجموعة `docker`: عضويتها تعادل صلاحية root. الأوامر في هذا الدليل تستعمل `sudo`.

## 3. المجلدات

```bash
sudo install -d -m 755 /opt/subul /opt/subul/releases
sudo install -d -m 755 -o <username> /opt/subul/incoming
sudo install -d -m 700 /srv/subul /srv/subul/backups /srv/subul/backups/database
sudo install -d -m 750 -o 1654 -g 1654 /srv/subul/img
sudo install -d -m 700 /etc/subul
```

- `/srv/subul/img` يملكه UID **1654**، وهو المستخدم الذي يعمل به الـ API داخل الحاوية. بغيره يفشل رفع الصور.
- مجلد النسخ الاحتياطية لـ root وحده، لأن فيه أسماء الزبائن وهواتفهم وعناوينهم.

## 4. بناء الإصدار على حاسوبك

مرة واحدة، أنشئ `.env.release` من القالب، وضع فيه IP السيرفر بشرطات بدل النقاط:

```powershell
Copy-Item .env.release.example .env.release
```

```
STORE_HOST=<a-b-c-d>.sslip.io
ADMIN_HOST=admin.<a-b-c-d>.sslip.io
PUBLIC_SITE_URL=https://<a-b-c-d>.sslip.io
PUBLIC_IMAGE_URL=
```

مثال: IP السيرفر `91.107.12.34` يصبح `91-107-12-34.sslip.io`.

ثم، مع Docker Desktop شغّال، ومن Git Bash في جذر المشروع:

```bash
bash deploy/scripts/release-export.sh
```

السكربت يرفض البناء إن وجد تغييرات لم تُحفظ في commit، أو ثغرة عالية أو حرجة في التبعيات، أو اختباراً فاشلاً. في النهاية يطبع اسم الملف وقيمة **SHA-256**، فاحتفظ بها للمقارنة بعد النقل.

## 5. النقل وملف الأسرار

**النقل:**

```powershell
scp deploy-out\subul-release-<tag>.tar <username>@<IP>:/opt/subul/incoming/
scp deploy\scripts\release-import.sh <username>@<IP>:/opt/subul/incoming/
```

أو عبر **FileZilla**: بروتوكول SFTP، المنفذ 22، ومفتاحك من Settings ← SFTP. ارفع الملفين إلى `/opt/subul/incoming/`.

**ملف الأسرار (أول مرة فقط):**

```bash
cd /opt/subul/incoming
tar -xOf subul-release-<tag>.tar subul-release-<tag>/production.env.example | sudo tee /etc/subul/production.env >/dev/null
sudo chmod 600 /etc/subul/production.env
```

ولّد كل سرّ بأمر مستقل. كل قيمة 64 حرفاً من أرقام وحروف فقط، وهذا مطلوب لكلمتَي مرور Redis:

```bash
openssl rand -hex 32
```

ثم `sudo nano /etc/subul/production.env` واملأ الحقول التالية:

| المتغير | القيمة |
|---|---|
| `POSTGRES_PASSWORD` | سرّ مولّد |
| `REDIS_PASSWORD`، `REDIS_CACHE_PASSWORD` | سرّان مولّدان **مختلفان** |
| `JWT_SECRET`، `AUTH_SECRET` | سرّان مولّدان مختلفان |
| `STORE_HOST`، `ADMIN_HOST`، `PUBLIC_SITE_URL` | **نفس** قيم `.env.release` حرفياً |
| `ADMIN_BOOTSTRAP_EMAIL` | بريدك |
| `ADMIN_BOOTSTRAP_PASSWORD` | كلمة مرور مؤقتة قوية (10 محارف على الأقل). ستُجبر على تغييرها عند أول دخول |
| `IMAGE_STORAGE_PROVIDER` | `Local` |
| `UPLOADS_PATH` | `/srv/subul/img` |

اترك الباقي كما هو، ومنه قيم R2 الفارغة حتى يتوفر الدومين.

## 6. أول تشغيل

```bash
sudo docker volume create --label com.subul.type=database --label com.subul.environment=production subul-production-postgres
sudo bash /opt/subul/incoming/release-import.sh /opt/subul/incoming/subul-release-<tag>.tar
sudo subul config --quiet && echo OK
sudo subul up -d
```

قارن قيمة SHA-256 التي يطبعها `release-import.sh` بالقيمة التي ظهرت على حاسوبك.

**تحقّق:**

```bash
sudo subul ps              # migrate: Exited (0)، والباقي Up، والواجهتان (healthy)
sudo subul logs migrate    # Applied 2 migration(s): ...
sudo subul logs api | grep -i bootstrap
sudo subul logs traefik | grep -i -E "acme|certificate|error"
```

- **المتجر:** `https://<a-b-c-d>.sslip.io`
- **لوحة الإدارة:** `https://admin.<a-b-c-d>.sslip.io`

الشهادة تصدر عند أول وصول لكل اسم، وقد يستغرق ذلك دقيقة. إن ظهر تحذير شهادة، راجع «استكشاف الأخطاء».

## 7. أول دخول

1. ادخل إلى لوحة الإدارة ببريد `ADMIN_BOOTSTRAP_EMAIL` وكلمة المرور المؤقتة. سيطلب منك النظام كلمة مرور جديدة.
2. **مهم:** بعد تغييرها احذف كلمة المرور المؤقتة من ملف الأسرار. المستخدم الأول لا يُنشأ مرة أخرى، لكن لا داعي لبقاء كلمة مرور في ملف:

   ```bash
   sudo sed -i 's/^ADMIN_BOOTSTRAP_PASSWORD=.*/ADMIN_BOOTSTRAP_PASSWORD=/; s/^ADMIN_BOOTSTRAP_ENABLED=.*/ADMIN_BOOTSTRAP_ENABLED=false/' /etc/subul/production.env
   sudo subul up -d
   ```

3. من اللوحة: أضف مناطق الشحن، ثم التصنيفات والعلامات والمنتجات، ثم حسابات الموظفين.

## 8. النسخ الاحتياطي

**جرّبه يدوياً:**

```bash
sudo /opt/subul/current/deploy/scripts/backup.sh
sudo ls -lh /srv/subul/backups/database
```

**جدولته كل ليلة (2:30 صباحاً):**

```bash
echo '30 2 * * * root /opt/subul/current/deploy/scripts/backup.sh >> /var/log/subul-backup.log 2>&1' | sudo tee /etc/cron.d/subul-backup
```

- يُحتفظ بآخر 14 يوماً (المتغير `KEEP_DAYS`).
- **نسخة على نفس القرص ليست نسخة احتياطية للسيرفر.** إلى أن نربط bucket خاصاً على R2، نزّل مجلد `/srv/subul/backups/database` إلى حاسوبك أسبوعياً على الأقل (FileZilla أو `scp`). وعند توفر R2، نضبط rclone ونملأ `BACKUP_OFFSITE_REMOTE`، فيُنسخ كل backup تلقائياً.

**الاسترجاع** (يستبدل قاعدة البيانات بالكامل، ويأخذ نسخة احتياطية من الحالية قبلها):

```bash
sudo /opt/subul/current/deploy/scripts/restore.sh subul-db-<stamp>.dump
```

## 9. التحديث إلى إصدار جديد

1. **على حاسوبك:** نفّذ commit لما عدّلته، ثم `bash deploy/scripts/release-export.sh`.
2. **النقل:** انقل الأرشيف إلى `/opt/subul/incoming/`.
3. **على السيرفر:**

   ```bash
   sudo /opt/subul/current/deploy/scripts/backup.sh
   sudo bash /opt/subul/current/deploy/scripts/release-import.sh /opt/subul/incoming/subul-release-<tag>.tar
   sudo subul up -d
   sudo subul ps && sudo subul logs migrate
   ```

`up -d` يشغّل `migrate` أولاً، فتُطبّق أي تعديلات على قاعدة البيانات، ثم تُستبدل الحاويات.

**الرجوع إلى الإصدار السابق** (يطبع `release-import.sh` الأمر الدقيق):

```bash
sudo ln -sfn /opt/subul/releases/subul-release-<old-tag> /opt/subul/current
sudo subul up -d
```

الرجوع يعمل مباشرة ما دامت migrations الإصدار الجديد تضيف ولا تحذف، وهذه قاعدة المشروع. وإن حذف الإصدار الجديد عموداً، فاسترجع أيضاً النسخة الاحتياطية التي أخذتها قبل التحديث.

**تنظيف الإصدارات القديمة** بعد أن تطمئن للجديد:

```bash
sudo rm -rf /opt/subul/releases/subul-release-<very-old-tag>
sudo docker image prune -a    # يحذف الصور غير المستخدمة فقط، لا البيانات
```

## 10. ربط الدومين لاحقاً (`.iq` على Cloudflare DNS)

1. أضف الدومين إلى Cloudflare، وغيّر الـ nameservers عند المسجِّل.
2. أنشئ سجلَّي **A** بـ IP السيرفر: `@` و`admin`، ومع كلٍّ منهما **DNS only (سحابة رمادية)**.
3. على حاسوبك: غيّر في `.env.release` القيم `STORE_HOST=<domain>` و`ADMIN_HOST=admin.<domain>` و`PUBLIC_SITE_URL=https://<domain>`، ثم ابنِ إصداراً جديداً.
4. على السيرفر: غيّر نفس القيم الثلاث في `/etc/subul/production.env`، ثم نفّذ خطوات «التحديث».

Traefik يصدر الشهادات الجديدة وحده. الانتقال إلى R2 مشروح في `docs/environments.md`، قسم «Moving images to Cloudflare R2».

## 11. أوامر يومية

```bash
sudo subul ps                    # حالة كل الحاويات
sudo subul logs -f --tail 100 api
sudo subul restart storefront
df -h / /srv                     # المساحة
sudo tail -n 20 /var/log/subul-backup.log
```

**لا تنفّذ أبداً** هذه الأوامر دون نسخة احتياطية متحقَّق منها وهدف محدد:
- `docker volume rm`
- `docker volume prune`
- `docker system prune --volumes`

الأمر `subul` يرفض `-v` أصلاً.

## 12. استكشاف الأخطاء

| العَرَض | السبب المرجّح والحل |
|---|---|
| تحذير شهادة في المتصفح | Let's Encrypt لم يصل إلى المنفذ 80. تحقق من: ufw، وجدار المزوّد، وأن الاسم يشير إلى IP السيرفر (`nslookup <name>`)، ثم `sudo subul logs traefik`. عند رسالة `rateLimited` انتظر ساعة، فلـ sslip.io حدود مشتركة |
| 404 على الـ IP مباشرة | سلوك مقصود: النظام يُفتح بالأسماء فقط |
| `migrate` لا يخرج بـ 0 | `sudo subul logs migrate`. الـ API لن يبدأ قبل أن تُحل المشكلة، وقاعدة البيانات لم تتغير |
| رفع الصور يفشل | ملكية `/srv/subul/img` ليست 1654: `sudo chown -R 1654:1654 /srv/subul/img` |
| الدخول إلى اللوحة يعطي «طلبات كثيرة» | إما 10 محاولات خاطئة خلال 5 دقائق من نفس العنوان، أو أن `redis` متوقف (يُرفض الدخول عمداً عند تعطّله): `sudo subul ps redis` |
| اللوحة تُخرجك باستمرار | `redis` متوقف (التحقق من الجلسات يرفض عند تعطّله)، أو الساعة غير مضبوطة: `timedatectl` |
