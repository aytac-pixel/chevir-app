using System.Collections.Generic;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Optional learned model (the language pack's small Transformer exported to TFLite/ONNX).
    /// When present, SignMatcher blends its probability with the template-matching score.
    /// </summary>
    public interface ISignClassifier
    {
        /// <summary>Returns false when the model cannot score this sign (e.g. not in its label set).</summary>
        bool TryScore(IReadOnlyList<SignFeatureFrame> frames, SignDefinition target, out float probability);
    }
}
