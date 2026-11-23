# Chevir: telefona quraşdırma

Bu qovluq tam Unity layihəsidir. Aşağıdakı addımlarla Android telefonda (və ya iPhone-da) işləyən tətbiq alırsan.

## 1. Lazım olanlar (bir dəfəlik)

- **Unity Hub** və **Unity 2022.3 LTS** (layihə 2022.3.45f1-də açılır, istənilən 2022.3 versiyası olar).
- Unity quraşdırarkən modullar: **Android Build Support** (içində *OpenJDK* və *Android SDK & NDK Tools* seçilsin). iPhone üçün Mac-da **iOS Build Support** və Xcode.
- Kompüterdə Python 3 (yalnız Mac/Linux skripti üçün) və ya Windows-da PowerShell.

## 2. MediaPipe plaginini yüklə

Layihə qovluğunda:

- **Windows (PowerShell):** `.\Tools\setup\install_mediapipe_plugin.ps1`
- **Mac / Linux:** `bash Tools/setup/install_mediapipe_plugin.sh`

Skript homuler/MediaPipeUnityPlugin-in son buraxılışını `Packages/` qovluğuna endirir və layihəyə əlavə edir. Əl ilə etmək istəsən: github.com/homuler/MediaPipeUnityPlugin/releases səhifəsindən `com.github.homuler.mediapipe-*.tgz` faylını endir, `Packages/` qovluğuna qoy və Unity-də *Window > Package Manager > + > Add package from tarball* ilə əlavə et.

El və üz modelləri (`hand_landmarker`, `face_landmarker`) layihəyə artıq daxildir.

## 3. Layihəni aç və bir kliklə qur

1. Unity Hub > **Add** > bu qovluğu seç və aç (ilk açılış bir neçə dəqiqə çəkir).
2. Menyuda **Chevir > Setup > 1. Configure Mobile Project (one click)**.
   - TextMeshPro resursları idxal olunarsa, eyni menyunu bir dəfə də bas.
   - Bu addım AzSLD dil paketini (A1 səviyyəsi, 10 bölmə, 10 hekayə), VT323 piksel şriftini (Ə daxil), əsas səhnəni (`Assets/Scenes/Main.unity`), dörd əsas ekranı (Dərs, Hekayə, Liqa, Profil), giriş/qeydiyyat ekranını, dərs və hekayə ekranlarını, Android/iOS ayarlarını (portret, kamera və internet icazəsi, loqodan tətbiq ikonu, `com.chevirapp.cevir`) hazırlayır.
3. **Play** düyməsinə bas. Verilənlər bazası qoşulubsa (aşağıda, 3a), əvvəlcə qeydiyyat/giriş ekranı açılır; sonra Dərs ekranı, 1-ci bölmə (Əlifba: 25 hərf, 5 dərs + təkrar; hərəkətli hərflər Ç, G, Ğ, Ö, Ü, Y, Z çıxarılıb) aktivdir. Kompüterin kamerası ilə də yoxlaya bilərsən.

## 3a. Verilənlər bazası (Supabase): hesablar, irəliləyiş və liqa

Tətbiq istifadəçiləri, onların irəliləyişini və liqa reytinqini Supabase-də saxlayır (pulsuz plan kifayətdir).

1. supabase.com-da hesab aç, **New project** yarat (region: Frankfurt / eu-central yaxındır). Verilənlər bazası şifrəsini bir yerdə saxla.
2. Layihədə **SQL Editor > New query**, `Tools/supabase/schema.sql` faylının bütün mətnini yapışdır və **Run** bas. Cədvəllər, giriş qaydaları, liqa funksiyaları, profil şəkilləri üçün `avatars` qovluğu və hesabı silmə funksiyası yaranır (təkrar işə salmaq təhlükəsizdir).
3. **Project Settings > API** (və ya *Data API*) bölməsindən **Project URL** və **anon public** açarını götür.
4. Bu iki dəyəri `Assets/Core/Resources/cevir_cloud.json` faylına yaz (`supabaseUrl`, `anonKey`). Bu açar açıq (public) açardır, tətbiqə yazmaq olar; **service_role** açarını heç vaxt tətbiqə qoyma.
5. **Authentication > Sign In / Providers > Email**: *Confirm email* açıqdır, istifadəçi e-poçtuna gələn linki açıb sonra daxil olur. *Authentication > URL Configuration* bölməsində Site URL olaraq `https://chevirapp.com` yaz.
6. **Authentication > Emails > Reset password** şablonuna `{{ .Token }}` əlavə et: şifrəni unudan istifadəçi e-poçtdakı 6 rəqəmli kodu tətbiqə yazıb yeni şifrə qoyur.

Bu addım edilməsə tətbiq yalnız telefonda işləyir: hesab, sinxronizasiya və liqa reytinqi olmur (Unity konsolunda xəbərdarlıq çıxır).

## 4. Telefona yığ

**Android (APK):**
1. *File > Build Settings > Android > Switch Platform* (bir dəfəlik).
2. **Chevir > Build > Android APK (install on phone)**. Fayl: `Builds/Android/Cevir.apk`.
3. APK-ni telefona köçür və aç (telefonda "naməlum mənbələrdən quraşdırma"ya icazə ver). Və ya telefonu USB ilə qoşub *File > Build And Run*.

**iPhone:** Mac-da **Chevir > Build > iOS Xcode Project**, sonra `Builds/iOS` qovluğunu Xcode-da aç, öz Apple hesabınla imzala və telefona yüklə.

**Google Play üçün:** **Chevir > Build > Android App Bundle (Google Play)**. Yayından əvvəl öz imza açarını (*Player Settings > Publishing Settings > Keystore*) yarat.

## 4a. Şəkil və videoları əlavə etmək

1. Faylları `Assets/LanguagePacks/AzSLD/Media/` qovluğuna, siyahıdakı kimi qoy: məs. `Media/A1/U01_Elifba/herf_a.png`.
2. Unity-də **Chevir > AzSLD > Import Media (pictures and videos)** (və ya **Chevir > Setup Project**). Konsolda neçə faylın bağlandığı və hansı adların heç bir işarəyə uyğun gəlmədiyi yazılır.
3. Qayda: `<ad>.png/.jpg` şəkil, `<ad>.mp4` video, `<ad>_yavas.mp4` yavaş video. Adlar `kurikulum/Media_siyahisi_A1_A2.csv` ilə eynidir (herf_ch, herf_i_noqtesiz, sagh_ol...). Alt qovluq vacib deyil, yalnız fayl adı uyğunlaşdırılır.
4. Ölçü: 720×960 (3:4, şaquli). Video H.264, səssiz, 2–5 san. Şəkil 300 KB-dan az.

Dərsdə video varsa video, yoxdursa şəkil göstərilir.

## 5. Kompütersiz, buludda APK (istəyə görə)

`.github/workflows/build-android.yml` `main`-ə hər push-dan sonra APK-ni GitHub-da avtomatik yığır və **Releases** səhifəsinə qoyur (ən son buraxılış həmişə ən təzə APK-dır):
- https://github.com/aytac-pixel/chevir-app/releases/latest
- Birbaşa yükləmə: https://github.com/aytac-pixel/chevir-app/releases/latest/download/Cevir.apk

Bunun üçün bir dəfə repozitoriyada *Settings > Secrets and variables > Actions* bölməsinə `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` əlavə et (pulsuz Unity Personal lisenziyası olar; game.ci/docs/github/activation). Əl ilə yenidən yığmaq üçün: *Actions > Build Android APK > Run workflow*.

## Hazırda nə işləyir, nə yox

- **İşləyir:** Dərs ekranındakı 10 bölməlik yol, dərslər, XP, seriya, Hekayə, Liqa, Profil, alt menyu, Azərbaycanca interfeys, kamera (cihazda), işarəni tanıma alqoritmi.
- **Canlar:** ən çox 10. Səhv işarə və ya səhv cavab 1 can aparır; hər 2 saatdan bir 1 can bərpa olunur (tətbiq bağlı olanda da). Təkrar dərsləri can aparmır və can qaytarır.
- **Bölmə 1 dərsi:** hərf göstərilir → **ANLADIM** → kamera açılır → düzgün göstərsən növbəti hərf, səhv olsa −1 can və eyni hərf yenidən.
- **Hesablar və liqa:** qeydiyyat (ad, e-poçt, şifrə), giriş, şifrəni unutdum, çıxış. İrəliləyiş hesabda saxlanır, başqa telefonda daxil olanda geri gəlir. Liqada eyni liqadakı real istifadəçilər həftəlik XP-yə görə sıralanır; həftə bitəndə yüksəlmə/enmə serverdə hesablanır. Saxta istifadəçi, izləyici və ya dost məlumatı yoxdur.
- **Hekayə 1 ("Hello, my name is..."):** 1-ci bölmə bitəndə açılır və dərhal təklif olunur. Aysel yaxınlaşır, salam verir, karların bir-birini ad işarəsi ilə çağırdığını deyir və ad işarəni soruşur; "İşarə dili ilə cavab ver" ön kameranı açır, ad işarəni (qeydiyyatda yazdığın ləqəb və ya ən tanınan xüsusiyyət) göstərirsən. Düzgün cavabda uğur animasiyası və "Nice to meet you!" gəlir, XP və seriya yazılır, hekayə "Completed" olur.
- **Hekayə 2–10:** adları və açılma qaydası hazırdır, səhnələri hələ yazılmayıb ("Tezliklə").
- **İstinad məlumatı olmayan hərflər:** hər hərf üçün istinad (əl nöqtələri) idxal olunmayıbsa, tətbiq yalnız əlin kamerada aydın görünməsini yoxlayır və bunu ekranda açıq yazır; əl formasını hələ yoxlamır. İstinad idxal olunanda (README, "Reference data pipeline") həmin hərf üçün əsl yoxlama özü işə düşür.
- **Hekayə şəkilləri və səsləri** sadə fayllardır, eyni adla əvəz oluna bilər.
- **Yoxlanılıb:** verilənlər bazası sxemi Postgres 16-da (cədvəllər, giriş qaydaları, liqa hesablaması).
- **Yoxlanılmayıb:** ekranların real Unity-də görünüşü (ölçü və boşluqlarda düzəliş lazım ola bilər), giriş və sinxronizasiyanın real Supabase layihəsi ilə işi, MediaPipe körpüsü plaginin real versiyası və real telefonda. Kamera bucağı və ya sağ/sol əl səhv görünərsə, `MediaPipeLandmarkProvider` komponentində *Flip Vertically* / *Invert Handedness* seçimlərini dəyiş.
