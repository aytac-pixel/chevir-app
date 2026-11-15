using Cevir.Core.Localization;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Interface text in English and Azerbaijani (see <see cref="Loc"/>). Kept in one place so a localization package can replace it later;
    /// recognition feedback text lives in <see cref="Recognition.FeedbackMessages"/>.
    /// </summary>
    public static class UIStrings
    {
        public static string Start => Loc.Pick("START", "BAŞLA");
        public static string Continue => Loc.Pick("CONTINUE", "DAVAM ET");
        public static string Retry => Loc.Pick("TRY AGAIN", "YENİDƏN CƏHD ET");
        public static string Check => Loc.Pick("CHECK", "YOXLA");
        public static string Close => Loc.Pick("CLOSE", "BAĞLA");
        public static string BackToMap => Loc.Pick("BACK HOME", "ƏSAS EKRANA QAYIT");
        public static string Practice => Loc.Pick("PRACTICE", "TƏKRAR ET");
        public static string Slow => Loc.Pick("Slow", "Yavaş");

        /// <summary>Playback speed label: 1 → "1x", 0.75 → "0.75x" (same in every language).</summary>
        public static string Speed(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
        }
        public static string Normal => Loc.Pick("Normal", "Normal");

        public static string WatchPrompt => Loc.Pick("Watch and remember", "Bax və yadda saxla");
        public static string GotIt => Loc.Pick("GOT IT", "ANLADIM");
        public static string SignPrompt => Loc.Pick("Show the sign", "İşarəni göstər");
        public static string RecognizePrompt => Loc.Pick("Which sign is this?", "Bu hansı işarədir?");
        public static string ReviewPrompt => Loc.Pick("Review", "Təkrar");
        public static string GatePrompt => Loc.Pick("Checkpoint", "İmtahan");

        public static string Calibrating => Loc.Pick("Look at the camera and keep your face relaxed...", "Kameraya düz bax, üzünü sakit saxla...");
        public static string WaitingForHand => Loc.Pick("Show your hand to the camera", "Əlini kameraya göstər");
        public static string Recording => Loc.Pick("Signing...", "Göstərirsən...");
        public static string Scoring => Loc.Pick("Checking...", "Yoxlanılır...");
        public static string PressToStart => Loc.Pick("Press START when you're ready", "Hazır olanda BAŞLA düyməsini bas");

        public static string CorrectTitle => Loc.Pick("Great!", "Əla!");
        public static string WrongTitle => Loc.Pick("Not quite", "Düzgün deyil");
        public static string TryAgainTitle => Loc.Pick("Try once more", "Bir daha cəhd et");
        public static string CorrectAnswerIs => Loc.Pick("Correct answer: ", "Düzgün cavab: ");

        public static string LessonComplete => Loc.Pick("Lesson complete!", "Dərs tamamlandı!");
        public static string GatePassed => Loc.Pick("Checkpoint passed!", "İmtahan keçildi!");
        public static string GateFailed => Loc.Pick("Checkpoint not passed", "İmtahan keçilmədi");
        public static string OutOfHeartsTitle => Loc.Pick("Out of hearts", "Canın qalmadı");
        public static string OutOfHeartsBody => Loc.Pick("A heart comes back every 2 hours. A practice lesson also wins hearts back.",
            "Hər 2 saatdan bir 1 can bərpa olunur. Təkrar dərsi ilə də can qazana bilərsən.");
        public static string NoHeartsToStart => Loc.Pick("No hearts left. The next one comes back in {0}, or do a practice lesson.",
            "Canın qalmayıb. Növbəti can {0} sonra gələcək, ya da təkrar dərsi et.");
        public static string NextHeartIn => Loc.Pick("+1 heart in {0}", "+1 can: {0}");

        /// <summary>"1 h 25 min" / "1 saat 25 dəq".</summary>
        public static string Duration(System.TimeSpan span)
        {
            int hours = (int)span.TotalHours;
            int minutes = System.Math.Max(hours > 0 ? 0 : 1, (int)System.Math.Ceiling(span.TotalMinutes - hours * 60));
            if (minutes == 60) { hours++; minutes = 0; }
            if (hours > 0) return minutes > 0 ? Loc.Pick(hours + " h " + minutes + " min", hours + " saat " + minutes + " dəq") : Loc.Pick(hours + " h", hours + " saat");
            return Loc.Pick(minutes + " min", minutes + " dəq");
        }
        public static string Locked => Loc.Pick("This unit is locked. Finish the earlier units first.", "Bu bölmə hələ bağlıdır. Əvvəlki bölmələri tamamla.");
        public static string Perfect => Loc.Pick("Perfect camera score! +5 XP", "Mükəmməl kamera nəticəsi! +5 XP");
        public static string StreakUp => Loc.Pick("Streak: {0} days", "Seriya: {0} gün");
        public static string Accuracy => Loc.Pick("Accuracy: {0}%", "Dəqiqlik: {0}%");
        public static string XpGained => Loc.Pick("+{0} XP", "+{0} XP");
        public static string HeartsRestored => Loc.Pick("+{0} hearts", "+{0} can");
        public static string Guidebook => Loc.Pick("Guidebook", "Bələdçi");
        public static string ComingSoon => Loc.Pick("This content is being prepared. Coming soon!", "Bu dərsin məzmunu hazırlanır. Tezliklə!");
        public static string SelfCheckDone => Loc.Pick("I SIGNED IT", "GÖSTƏRDİM");
        public static string SelfCheckHint => Loc.Pick("Automatic checking for this sign isn't ready yet. Watch yourself in the camera, sign it, then confirm.", "Bu işarənin avtomatik yoxlaması hələ hazır deyil. Kamerada özünə baxaraq göstər və təsdiqlə.");

        public static string ThirdFailRequeue => Loc.Pick("We'll come back to this sign later.", "Bu işarəyə sonra qayıdacağıq.");
        public static string CameraStarting => Loc.Pick("Starting camera...", "Kamera açılır...");
        public static string CameraDenied => Loc.Pick("Chevir needs the camera.\nAllow it in Settings > Apps > Chevir.", "Kameraya icazə lazımdır.\nTelefonun Ayarlar > Tətbiqlər > Chevir bölməsində kameranı aç.");
        public static string CameraMissing => Loc.Pick("The camera module isn't installed.", "Kamera modulu quraşdırılmayıb.");

        // ---------------------------------------------------------------- tabs and home
        public static string TabHome => Loc.Pick("Lessons", "Dərs");
        public static string TabStories => Loc.Pick("Stories", "Hekayə");
        public static string TabLeague => Loc.Pick("League", "Liqa");
        public static string TabProfile => Loc.Pick("Profile", "Profil");
        public static string LevelKicker => Loc.Pick("LEVEL", "SƏVİYYƏ");
        public static string LevelTitle => Loc.Pick("{0} Level", "{0} Səviyyə");
        public static string UnitsProgress => Loc.Pick("{0} / {1} units", "{0} / {1} bölmə");
        public static string UnitKicker => Loc.Pick("UNIT {0}", "BÖLMƏ {0}");
        public static string Entrance => Loc.Pick("ENTRANCE", "GİRİŞ");
        public static string UnitLessons => Loc.Pick("Lesson {0} of {1}", "Dərs {0} / {1}");
        public static string UnitDone => Loc.Pick("Unit complete. Practise any lesson again.", "Bölmə tamamlandı. İstənilən dərsi təkrar keçə bilərsən.");
        public static string CheckpointBody => Loc.Pick("Review everything from {0} and pass the checkpoint ({1}% correct) to enter the next level.",
            "{0} səviyyəsini təkrarla və növbəti səviyyəyə keçmək üçün imtahanı ({1}% düzgün) keç.");
        public static string PracticeAgain => Loc.Pick("PRACTISE AGAIN", "YENİDƏN KEÇ");

        // ---------------------------------------------------------------- stories
        public static string StoriesKicker => Loc.Pick("STORIES", "HEKAYƏLƏR");
        public static string StoriesTitle => Loc.Pick("Sign Language Stories", "İşarə Dili Hekayələri");
        public static string StoriesLevel => Loc.Pick("{0} Level", "{0} Səviyyə");
        public static string StoriesBanner => Loc.Pick("Complete lessons on the Home screen\n<b>to unlock new stories!</b>", "Əsas ekranda dərsləri tamamla\n<b>və yeni hekayələr aç!</b>");
        public static string StoryLocked => Loc.Pick("LOCKED", "BAĞLI");
        public static string StoryNew => Loc.Pick("NEW · START", "YENİ · BAŞLA");
        public static string StoryCompleted => Loc.Pick("COMPLETED", "TAMAMLANDI");
        public static string StorySoon => Loc.Pick("COMING SOON", "TEZLİKLƏ");
        public static string StoryLockedToast => Loc.Pick("Finish Unit {0} on the Home screen to unlock this story.", "Bu hekayəni açmaq üçün Əsas ekranda {0}-ci bölməni bitir.");
        public static string StoryUnlockedTitle => Loc.Pick("New story unlocked!", "Yeni hekayə açıldı!");
        public static string StoryUnlockedBody => Loc.Pick("Practise what you learned in a real conversation:\n\"{0}\"", "Öyrəndiklərini real söhbətdə sına:\n\"{0}\"");
        public static string StartStory => Loc.Pick("START STORY", "HEKAYƏNİ BAŞLA");
        public static string Later => Loc.Pick("LATER", "SONRA");
        public static string RespondInSign => Loc.Pick("Respond in Sign Language", "İşarə dili ilə cavab ver");
        public static string TapToContinue => Loc.Pick("Tap to continue", "Davam etmək üçün toxun");
        public static string SpellYourName => Loc.Pick("Fingerspell your name", "Adını daktil ilə göstər");
        public static string SignThisLetter => Loc.Pick("Sign the letter", "Hərfi göstər");
        public static string SkipLetter => Loc.Pick("Skip letter", "Hərfi keç");
        public static string CloseCamera => Loc.Pick("Close camera", "Kameranı bağla");
        public static string ShowHandsClearly => Loc.Pick("Show your hands clearly to the camera", "Əllərini kameraya aydın göstər");
        public static string AdjustFingerAngle => Loc.Pick("Adjust your finger angle", "Barmaq bucağını düzəlt");
        public static string LetterSelfCheck => Loc.Pick("Automatic checking for this letter isn't ready yet: we only check that your hand is clearly visible.",
            "Bu hərfin avtomatik yoxlaması hələ hazır deyil: yalnız əlinin aydın göründüyünü yoxlayırıq.");
        public static string NameCardTitle => Loc.Pick("What's your name?", "Adın nədir?");
        public static string NameCardBody => Loc.Pick("You'll spell it in sign language, letter by letter.", "Onu işarə dilində, hərf-hərf göstərəcəksən.");
        public static string FirstName => Loc.Pick("Name", "Ad");
        public static string LastName => Loc.Pick("Surname", "Soyad");
        public static string StoryComplete => Loc.Pick("Story complete!", "Hekayə tamamlandı!");
        public static string StoryCompleteBody => Loc.Pick("\"{0}\" is now marked as completed.", "\"{0}\" tamamlandı kimi qeyd olundu.");
        public static string StoryPerfect => Loc.Pick("Every answer camera-checked! +{0} XP", "Bütün cavablar kamera ilə yoxlandı! +{0} XP");
        public static string BackToStories => Loc.Pick("BACK TO STORIES", "HEKAYƏLƏRƏ QAYIT");
        public static string SignsLabel => Loc.Pick("In sign language", "İşarə dilində");

        // ---------------------------------------------------------------- league
        public static string WeeklyRanking => Loc.Pick("WEEKLY RANKING", "HƏFTƏLİK REYTİNQ");
        public static string TopAdvance => Loc.Pick("TOP {0} ADVANCE TO THE NEXT LEAGUE", "İLK {0} NÖVBƏTİ LİQAYA KEÇİR");
        public static string BottomDemoted => Loc.Pick("BOTTOM {0} ARE DEMOTED", "SON {0} AŞAĞI DÜŞÜR");
        public static string XpUnit => "XP";

        // ---------------------------------------------------------------- profile
        public static string Joined => Loc.Pick("Joined {0}", "Qoşulub: {0}");
        public static string EditProfile => Loc.Pick("Edit profile", "Profili dəyiş");
        public static string WeeklyXp => Loc.Pick("XP this week", "Bu həftə XP");
        public static string LeaguePlace => Loc.Pick("League place", "Liqada yer");
        public static string LeagueOffline => Loc.Pick("Connect to the internet to see the ranking.", "Reytinqi görmək üçün internetə qoşul.");
        public static string LeagueAlone => Loc.Pick("No one else in this league yet this week.", "Bu həftə liqada hələ başqa iştirakçı yoxdur.");
        public static string Account => Loc.Pick("Account", "Hesab");
        public static string SignOut => Loc.Pick("SIGN OUT", "HESABDAN ÇIX");
        public static string SignOutTitle => Loc.Pick("Sign out?", "Hesabdan çıxırsan?");
        public static string SignOutBody => Loc.Pick("Your progress is saved in your account. Sign in again to continue.",
            "İrəliləyişin hesabında saxlanılıb. Davam etmək üçün yenidən daxil ol.");
        public static string Statistics => Loc.Pick("Statistics", "Statistika");
        public static string DayStreak => Loc.Pick("Day Streak", "Günlük seriya");
        public static string TotalXp => Loc.Pick("Total XP", "Ümumi XP");
        public static string LevelLabel => Loc.Pick("Level", "Səviyyə");
        public static string SignsLearned => Loc.Pick("Signs Learned", "Öyrənilən işarə");
        public static string Friends => Loc.Pick("Friends", "Dostlar");
        public static string FriendsTitle => Loc.Pick("Learning together is more fun!", "Birlikdə öyrənmək daha maraqlıdır!");
        public static string FriendsBody => Loc.Pick("Invite friends and share your streak.", "Dostlarını dəvət et və seriyanı paylaş.");
        public static string InviteFriends => Loc.Pick("Invite friends", "Dostları dəvət et");
        public static string InviteMessage => Loc.Pick("I'm learning Azerbaijani Sign Language with Chevir. My streak: {0} days! Join me: {1}",
            "Chevir ilə Azərbaycan İşarə Dilini öyrənirəm. Seriyam: {0} gün! Sən də qoşul: {1}");
        public static string LinkCopied => Loc.Pick("Invite link copied", "Dəvət linki kopyalandı");
        public static string Save => Loc.Pick("SAVE", "YADDA SAXLA");
        public static string AppLanguage => Loc.Pick("App language", "Tətbiqin dili");
        public static string SigningHand => Loc.Pick("Signing hand", "İşarə əli");
        public static string RightHand => Loc.Pick("Right", "Sağ");

        // ---------------------------------------------------------------- accounts
        public static string AuthTagline => Loc.Pick("Learn Azerbaijani Sign Language", "Azərbaycan İşarə Dilini öyrən");
        public static string SignInTitle => Loc.Pick("Sign in", "Daxil ol");
        public static string SignUpTitle => Loc.Pick("Create an account", "Hesab yarat");
        public static string SignInButton => Loc.Pick("SIGN IN", "DAXİL OL");
        public static string SignUpButton => Loc.Pick("CREATE ACCOUNT", "HESAB YARAT");
        public static string SwitchToSignUp => Loc.Pick("No account yet? Create one", "Hesabın yoxdur? Yarat");
        public static string SwitchToSignIn => Loc.Pick("Already have an account? Sign in", "Hesabın var? Daxil ol");
        public static string ForgotPassword => Loc.Pick("Forgot password?", "Şifrəni unutmusan?");
        public static string ResetSent => Loc.Pick("We e-mailed you a 6-digit code. Enter it and a new password.", "E-poçtuna 6 rəqəmli kod göndərdik. Kodu və yeni şifrəni yaz.");
        public static string ConfirmEmailSent => Loc.Pick("Account created. Open the link we sent to your e-mail, then sign in.",
            "Hesab yaradıldı. E-poçtuna göndərilən linki aç, sonra daxil ol.");
        public static string NeedName => Loc.Pick("Enter your name.", "Adını yaz.");
        public static string NeedEmail => Loc.Pick("Enter your e-mail.", "E-poçtunu yaz.");
        public static string NeedPassword => Loc.Pick("The password must be at least 6 characters.", "Şifrə ən azı 6 simvol olmalıdır.");
        public static string PleaseWait => Loc.Pick("Please wait...", "Gözlə...");
        public static string LeftHand => Loc.Pick("Left", "Sol");
    }

    /// <summary>
    /// Light blue / cyan "glass" theme from the design. Green, amber and red stay for lesson feedback, where
    /// they carry meaning (correct / tracking problem / mistake).
    /// </summary>
    public static class UIPalette
    {
        public static readonly Color Background = new Color32(0xF4, 0xF9, 0xFF, 0xFF);
        public static readonly Color Surface = new Color32(0xF1, 0xF6, 0xFC, 0xFF);
        public static readonly Color Border = new Color32(0xDD, 0xE8, 0xF5, 0xFF);
        public static readonly Color TextPrimary = new Color32(0x1E, 0x2A, 0x3B, 0xFF);
        public static readonly Color TextSecondary = new Color32(0x7A, 0x86, 0x99, 0xFF);
        public static readonly Color TextMuted = new Color32(0xA3, 0xAF, 0xC0, 0xFF);

        public static readonly Color Primary = new Color32(0x00, 0x6E, 0xF0, 0xFF); // logo blue
        public static readonly Color PrimaryDark = new Color32(0x00, 0x26, 0xC4, 0xFF); // logo deep blue
        public static readonly Color Cyan = new Color32(0x1E, 0xB9, 0xF0, 0xFF);
        public static readonly Color CyanLight = new Color32(0xE4, 0xF6, 0xFE, 0xFF);
        public static readonly Color Glass = new Color(1f, 1f, 1f, 0.82f);
        public static readonly Color GlassBorder = new Color32(0xDC, 0xEB, 0xFA, 0xFF);
        public static readonly Color Shadow = new Color(0.36f, 0.55f, 0.82f, 0.16f);
        public static readonly Color PathDot = new Color32(0xD3, 0xE5, 0xF6, 0xFF);

        public static readonly Color LockedFill = new Color32(0xE3, 0xE9, 0xF1, 0xFF);
        public static readonly Color LockedEdge = new Color32(0xC9, 0xD4, 0xE2, 0xFF);
        public static readonly Color LockedIcon = new Color32(0x7D, 0x8C, 0xA3, 0xFF);
        public static readonly Color DemoteRow = new Color32(0xFD, 0xEC, 0xEE, 0xFF);

        public static readonly Color Green = new Color32(0x2B, 0xB6, 0x73, 0xFF);
        public static readonly Color GreenDark = new Color32(0x1E, 0x8A, 0x55, 0xFF);
        public static readonly Color GreenLight = new Color32(0xDD, 0xF6, 0xE9, 0xFF);

        public static readonly Color Red = new Color32(0xEF, 0x4D, 0x5A, 0xFF);
        public static readonly Color RedDark = new Color32(0xC9, 0x2F, 0x3D, 0xFF);
        public static readonly Color RedLight = new Color32(0xFD, 0xE4, 0xE6, 0xFF);

        public static readonly Color Amber = new Color32(0xFF, 0xB8, 0x1F, 0xFF);
        public static readonly Color AmberDark = new Color32(0xB9, 0x7A, 0x00, 0xFF);
        public static readonly Color AmberLight = new Color32(0xFF, 0xF3, 0xD1, 0xFF);

        public static readonly Color Blue = Primary;
        public static readonly Color Heart = new Color32(0xF0, 0x44, 0x5A, 0xFF);
        public static readonly Color Streak = new Color32(0xFF, 0x8A, 0x1F, 0xFF);
        public static readonly Color Xp = new Color32(0x1B, 0xA9, 0xF5, 0xFF);
        public static readonly Color Gold = new Color32(0xF2, 0xB5, 0x2E, 0xFF);
        public static readonly Color Silver = new Color32(0x9A, 0xA8, 0xBA, 0xFF);
        public static readonly Color Bronze = new Color32(0xC0, 0x7A, 0x45, 0xFF);
    }
}
