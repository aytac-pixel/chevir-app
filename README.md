<p align="center">
  <img src="docs/images/logo.png" width="140" alt="Chevir I/O logo">
</p>

<h1 align="center">Chevir I/O</h1>

<p align="center">
  <b>Azərbaycan işarə dilini oyun kimi öyrən.</b><br>
  Kamera qarşısında işarə göstər, tətbiq onu telefonda dərhal yoxlasın.
</p>

<p align="center">
  <a href="https://github.com/aytac-pixel/chevir-io/releases/latest/download/ChevirIO.apk"><b>Android APK-nı yüklə</b></a> ·
  <a href="https://chevirapp.com">chevirapp.com</a> ·
  <a href="https://chevirapp.com/tercume/">Onlayn tərcüməçi</a> ·
  <a href="https://www.instagram.com/chevir_app/">Instagram @chevir_app</a>
</p>

---

## Niyə Chevir I/O?

Azərbaycanda minlərlə eşitmə qüsurlu insan gündəlik ünsiyyətdə işarə dilindən istifadə edir, amma onların ailələri, dostları və müəllimləri üçün bu dili öyrənməyin rahat yolu demək olar ki, yoxdur. Kitab və videolar işarəni göstərir, lakin **səni yoxlamır**.

Chevir I/O bu boşluğu doldurur: öyrənən işarəni telefonun ön kamerasına göstərir, tətbiq əl, barmaq və üz hərəkətlərini **cihazın özündə** analiz edir və dərhal "Düzgün!" və ya "Barmaq bucağını düzəlt" kimi konkret məsləhət verir. Hamısı qısa dərslər, hekayələr, alov seriyası və liqalarla, hər gün qayıtmaq istədiyin bir oyun formasında.

## Əsas imkanlar

| | |
|---|---|
| **Kamera ilə işarə tanıma** | MediaPipe əl, üz və bədən nöqtələri telefonda real vaxtda hesablanır. Video heç yerə göndərilmir. Əl forması, hərəkət trayektoriyası, yer və mimika ayrı-ayrı qiymətləndirilir. |
| **A1 kursu** | Əlifbadan başlayıb salamlaşma, əvəzliklər, sual sözləri, ailə, rəqəmlər, vaxt, əşyalar və vacib yerlərə qədər 10 bölmə. Hər işarə üçün real video, şəkil və yavaş göstərmə var. |
| **İnteraktiv hekayələr** | 10 piksel-art səhnəsi: personajla danışırsan, cavabı işarə dilində kameraya göstərirsən. Hekayədə öz **ad işarən (ləqəbin)** ilə tanış olursan, karlar bir-birini belə çağırır. |
| **Motivasiya sistemi** | XP, gündəlik alov seriyası, 10 can (hər 12 dəqiqədən bir +1 can, 2 saata tam dolur, oynayarkən də), səviyyələr və təkrar dərsləri. |
| **Liqalar** | Həftəlik, aylıq və illik reytinq, profil şəkilləri, Bürüncdən Almaza qədər 5 liqa, həftənin sonunda yüksəlmə və enmə. |
| **Statistika** | Hər gün neçə dəqiqə məşğul olduğun, yığdığın XP, ən uzun alov, səviyyə və öyrəndiyin işarələrin sayı. |
| **Canlı fon** | Dərs ekranının arxasında Bakı panoraması saata (səhər, günorta, axşam, gecə) və real havaya (yağış, qar) görə dəyişir. |
| **Ağıllı bildirişlər** | Həftənin hər günü üçün fərqli səhər və gecə salamı, öz seçdiyin saatlarda məşq xatırlatması və hər səhər liqada neçə pillə qalxdığın barədə bir mesaj. Tətbiqə artıq girmisənsə, artıq xatırlatma gəlmir. |
| **3 dil** | Bütün interfeys, dərslər, hekayələr və bildirişlər Azərbaycan, ingilis və rus dillərində. Dil istənilən vaxt Profildən dəyişir. |
| **Gecə rejimi** | Gündüz və gecə temaları, göz yormayan Nunito şrifti və piksel-art üslubu. |
| **Hesab və sinxronizasiya** | E-poçtla qeydiyyat, telefonlar arasında irəliləyişin saxlanması, profil şəkli, ad, ləqəb və telefon nömrəsi. |
| **Məxfilik və etik məlumat toplama** | İşarə nümunələri yalnız istifadəçinin açıq razılığı ilə saxlanılır və istənilən vaxt silinə bilər. Telefon nömrəsi yalnız sahibinə görünür. |

## Necə işləyir

```
 Ön kamera ──► MediaPipe (əl + üz + bədən nöqtələri, telefonda)
                    │
                    ▼
       Xüsusiyyət çıxarma: 26 əl forması dəyəri, bilək trayektoriyası,
       üzə görə yer, mimika (qaş, ağız, baş hərəkəti)
                    │
                    ▼
       Müqayisə: dərs videolarından çıxarılmış istinad nümunələri ilə
       DTW (dinamik zaman uyğunlaşdırma) + hərf modeli
                    │
                    ▼
       Qərar və məsləhət: "Düzgün!", "Barmaq bucağını düzəlt",
       "Hərəkəti yuxarıdan başla", "Mimikanı əlavə et"
```

- **Telefonda işləyir:** tanıma internet olmadan da işləyir, kamera görüntüsü telefondan çıxmır.
- **Ədalətli qiymətləndirmə:** kameranın əli görməməsi kimi texniki problemlər səhv sayılmır və can aparmır.
- **Solaxaylar üçün:** işarə əli Profildən seçilir, hərəkətlər avtomatik güzgülənir.
- **Modul quruluş:** hər işarə dili ayrıca "dil paketi"dir. Əsas kod dəyişmədən rus işarə dili və digər dillər əlavə oluna bilər.

## Texnologiyalar

**Unity 2022.3 LTS** (C#) · **MediaPipe** (cihazda landmark aşkarlama) · **Supabase** (Postgres, hesablar, sətir səviyyəsində təhlükəsizlik, saxlama) · **Android yerli plaginləri** (Java: arxa planda liqa yoxlaması) · **Python** (istinad məlumatlarının çıxarılması, piksel-art generatorları) · **GitHub Actions** (hər yeniləmədə avtomatik APK)

## Yüklə və sına

1. Android telefonda [**ChevirIO.apk**](https://github.com/aytac-pixel/chevir-io/releases/latest/download/ChevirIO.apk) faylını yüklə.
2. Faylı aç və quraşdırmaya icazə ver.
3. Hesab yarat, kameraya icazə ver və ilk hərfini göstər.

Tələb: Android 7.0 və ya daha yeni, 64-bit telefon.

## Tərtibatçılar üçün

Layihənin quruluşu, Unity-də açılması, verilənlər bazası və tanıma alqoritminin təfərrüatları: [**docs/DEVELOPERS.md**](docs/DEVELOPERS.md). Addım-addım quraşdırma (Azərbaycan dilində): [**QURASDIRMA.md**](QURASDIRMA.md).

---

## In English

**Chevir I/O** is a gamified mobile app for learning **Azerbaijani Sign Language**. Learners sign in front of the phone's camera and get instant, specific feedback. Hand, face and body landmarks are tracked **on the device** with MediaPipe and compared with reference recordings from real lesson videos. Features: a full A1 course in 10 units, interactive pixel-art stories where you answer in sign language, XP, streaks, hearts, weekly, monthly and yearly leagues, daily statistics, a sky that follows the time of day and the real weather, smart notifications, a night theme, and a full **Azerbaijani, English and Russian** interface. Built with Unity, MediaPipe and Supabase. Sign data is collected only with explicit consent.

[Download the Android APK](https://github.com/aytac-pixel/chevir-io/releases/latest/download/ChevirIO.apk) · [Website](https://chevirapp.com) · [Developer guide](docs/DEVELOPERS.md)

<p align="center"><sub>Chevir I/O komandası tərəfindən hazırlanıb · chevirapp.com</sub></p>
