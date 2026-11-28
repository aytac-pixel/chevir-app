using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using UnityEngine;

namespace Cevir.Core.Stories
{
    public enum StoryBeatType
    {
        /// <summary>The NPC speaks (voice + speech bubble) and signs.</summary>
        NpcLine,

        /// <summary>The learner fingerspells their own name with the alphabet from the pack.</summary>
        LearnerFingerspellName,

        /// <summary>The learner answers with one sign from the dictionary.</summary>
        LearnerSign,

        /// <summary>
        /// The learner shows their own name sign (ad işarəsi): in sign language people also call each other by a sign for a nickname or
        /// their most recognisable feature instead of spelling the name. The feature comes from the profile.
        /// </summary>
        LearnerNameSign
    }

    public enum NpcPose
    {
        Idle,
        Wave
    }

    public enum NameScope
    {
        FirstName,
        FullName
    }

    [Serializable]
    public struct LocalizedAudio
    {
        public AudioClip en;
        public AudioClip az;

        public AudioClip Get()
        {
            return Loc.IsAz && az != null ? az : (en != null ? en : az);
        }
    }

    /// <summary>One step of a story: an NPC line or a learner response.</summary>
    [Serializable]
    public sealed class StoryBeat
    {
        public StoryBeatType type = StoryBeatType.NpcLine;

        [Tooltip("NPC line, or the instruction shown for a learner beat. {name} is replaced by the learner's first name, {namesign} by their name sign.")]
        public LocalizedString text;

        [Tooltip("Voice for the NPC line. Optional: the bubble and the signs carry the line without it.")]
        public LocalizedAudio voice;

        [Tooltip("Signs the NPC performs after speaking, played in order from each sign's reference clip.")]
        public List<SignDefinition> npcSigns = new List<SignDefinition>();

        public NpcPose pose = NpcPose.Idle;

        [Tooltip("The NPC walks up to the learner before this line (first line of a scene).")]
        public bool approach;

        [Tooltip("LearnerFingerspellName only.")]
        public NameScope nameScope = NameScope.FullName;

        [Tooltip("LearnerSign only.")]
        public SignDefinition expectedSign;
    }

    /// <summary>A character in a story scene: name and the 2D poses the POV scene swaps between.</summary>
    [Serializable]
    public sealed class StoryCharacter
    {
        public LocalizedString displayName;
        public Sprite idle;
        public Sprite talking;
        public Sprite wave;
        public Sprite waveTalking;
    }

    /// <summary>
    /// A first-person practice story. Stories unlock when a unit on the home path is finished and are replayable.
    /// Content lives in the language pack (like lessons); the player is <c>StoryScreenUI</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/Curriculum/Story", fileName = "Story_")]
    public sealed class StoryData : CurriculumAsset
    {
        [Min(1)]
        [SerializeField] private int number = 1;

        [Tooltip("Finishing every lesson of this unit (or passing its level's gate) unlocks the story.")]
        [SerializeField] private UnitData unlockUnit;

        [Min(0)]
        [SerializeField] private int xpReward = 20;

        [Header("Scene")]
        [SerializeField] private Sprite background;
        [Tooltip("First-person foreground, e.g. the learner's hands holding a bag strap.")]
        [SerializeField] private Sprite foreground;
        [SerializeField] private StoryCharacter npc = new StoryCharacter();

        [SerializeField] private List<StoryBeat> beats = new List<StoryBeat>();

        public int Number => number;
        public UnitData UnlockUnit => unlockUnit;
        public int XpReward => xpReward;
        public Sprite Background => background;
        public Sprite Foreground => foreground;
        public StoryCharacter Npc => npc;
        public IReadOnlyList<StoryBeat> Beats => beats;

        /// <summary>Stories without beats are placeholders: listed, but not playable yet.</summary>
        public bool IsPlayable => beats.Count > 0;

#if UNITY_EDITOR
        public void EditorSetContent(int storyNumber, UnitData unit, int xp, Sprite bg, Sprite fg, StoryCharacter character, List<StoryBeat> storyBeats)
        {
            number = storyNumber;
            unlockUnit = unit;
            xpReward = xp;
            background = bg;
            foreground = fg;
            npc = character;
            beats = storyBeats;
        }

        /// <summary>Scene art only (each story has its own pixel-art place; the NPC and the hands are shared).</summary>
        public void EditorSetArt(Sprite bg, Sprite fg)
        {
            background = bg;
            foreground = fg;
        }

        public void EditorSetUnlock(int storyNumber, UnitData unit)
        {
            number = storyNumber;
            unlockUnit = unit;
        }
#endif
    }
}
