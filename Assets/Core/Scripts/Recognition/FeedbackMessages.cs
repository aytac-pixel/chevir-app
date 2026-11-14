using System.Collections.Generic;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Learner-facing feedback text by UI language. Sign language and UI language are separate:
    /// an RSL pack might still be taught in Azerbaijani, so this is keyed by the manifest's uiLanguage.
    /// </summary>
    public static class FeedbackMessages
    {
        private static readonly Dictionary<string, Dictionary<SignFeedback, string>> Tables =
            new Dictionary<string, Dictionary<SignFeedback, string>>
            {
                {
                    "az", new Dictionary<SignFeedback, string>
                    {
                        { SignFeedback.Correct, "Düzgün!" },
                        { SignFeedback.AdjustHandShape, "Barmaq bucağını düzəlt" },
                        { SignFeedback.AdjustMovement, "Hərəkəti yuxarıdan başla" },
                        { SignFeedback.AddFacialExpression, "Mimikanı əlavə et" },
                        { SignFeedback.ShowHandToCamera, "Əlini kameraya göstər" },
                        { SignFeedback.Unsure, "Əmin deyiləm, bir daha göstər" }
                    }
                },
                {
                    "ru", new Dictionary<SignFeedback, string>
                    {
                        { SignFeedback.Correct, "Верно!" },
                        { SignFeedback.AdjustHandShape, "Поправь положение пальцев" },
                        { SignFeedback.AdjustMovement, "Начни движение сверху" },
                        { SignFeedback.AddFacialExpression, "Добавь мимику" },
                        { SignFeedback.ShowHandToCamera, "Покажи руку камере" },
                        { SignFeedback.Unsure, "Не уверен, покажи ещё раз" }
                    }
                },
                {
                    "en", new Dictionary<SignFeedback, string>
                    {
                        { SignFeedback.Correct, "Correct!" },
                        { SignFeedback.AdjustHandShape, "Adjust your finger angle" },
                        { SignFeedback.AdjustMovement, "Start the movement from the top" },
                        { SignFeedback.AddFacialExpression, "Add the facial expression" },
                        { SignFeedback.ShowHandToCamera, "Show your hands clearly to the camera" },
                        { SignFeedback.Unsure, "Not sure, try once more" }
                    }
                }
            };

        public static string Get(SignFeedback feedback, string uiLanguage)
        {
            Dictionary<SignFeedback, string> table;
            if (string.IsNullOrEmpty(uiLanguage) || !Tables.TryGetValue(uiLanguage, out table))
            {
                table = Tables["az"];
            }

            string text;
            return table.TryGetValue(feedback, out text) ? text : feedback.ToString();
        }
    }
}
