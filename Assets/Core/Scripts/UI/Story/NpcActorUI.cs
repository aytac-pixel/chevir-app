using System.Collections;
using Cevir.Core.Stories;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// The 2D story character in the first-person scene: walks up from the distance (grows and lowers toward the
    /// camera with a step bob), breathes while idle, waves, and flaps the mouth sprite while talking.
    /// </summary>
    public sealed class NpcActorUI : MonoBehaviour
    {
        [SerializeField] private Image body;
        [SerializeField] private float farScale = 0.42f;
        [SerializeField] private float farLift = 300f;
        [SerializeField] private float mouthRate = 7f;

        private StoryCharacter _character;
        private NpcPose _pose;
        private bool _talking;
        private Vector2 _home;
        private bool _homeSet;
        private bool _walking;

        private RectTransform Rt => (RectTransform)transform;

        private void Awake()
        {
            CaptureHome();
        }

        private void CaptureHome()
        {
            if (_homeSet) return;
            _home = Rt.anchoredPosition;
            _homeSet = true;
        }

        public void Bind(StoryCharacter character)
        {
            CaptureHome();
            _character = character;
            _pose = NpcPose.Idle;
            _talking = false;
            SetFar();
            ApplySprite(false);
        }

        public void SetFar()
        {
            Rt.localScale = Vector3.one * farScale;
            Rt.anchoredPosition = _home + new Vector2(0f, farLift);
        }

        public void SetNear()
        {
            Rt.localScale = Vector3.one;
            Rt.anchoredPosition = _home;
        }

        public IEnumerator Approach(float seconds)
        {
            _walking = true;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3.2f)) * 18f * (1f - k * 0.6f);
                Rt.localScale = Vector3.one * Mathf.Lerp(farScale, 1f, k);
                Rt.anchoredPosition = _home + new Vector2(0f, Mathf.Lerp(farLift, 0f, k) + bob);
                yield return null;
            }

            _walking = false;
            SetNear();
        }

        public void SetPose(NpcPose pose)
        {
            _pose = pose;
            ApplySprite(false);
        }

        public void SetTalking(bool talking)
        {
            _talking = talking;
            if (!talking) ApplySprite(false);
        }

        private void Update()
        {
            if (_character == null || body == null) return;
            bool mouthOpen = _talking && Mathf.Repeat(Time.time * mouthRate, 1f) < 0.5f;
            ApplySprite(mouthOpen);

            if (!_walking)
            {
                // Idle breathing.
                float breath = 1f + Mathf.Sin(Time.time * 2.1f) * 0.008f;
                Rt.localScale = new Vector3(Rt.localScale.x, Mathf.Abs(Rt.localScale.x) * breath, 1f);
            }
        }

        private void ApplySprite(bool mouthOpen)
        {
            if (_character == null || body == null) return;
            Sprite sprite;
            if (_pose == NpcPose.Wave) sprite = mouthOpen && _character.waveTalking != null ? _character.waveTalking : _character.wave;
            else sprite = mouthOpen && _character.talking != null ? _character.talking : _character.idle;
            if (sprite == null) sprite = _character.idle;
            if (body.sprite != sprite) body.sprite = sprite;
        }
    }
}
