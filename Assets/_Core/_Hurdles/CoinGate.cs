using System;
using _Core._Player;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A gate that changes the coins collected this run: +N, -N, xN or ÷N.
    /// Good ones (add / multiply) show the green visual, bad ones (subtract / divide) the red one.
    /// Put two under a HurdleGroup for a left/right choice (see CoinGatePair). The label is made
    /// automatically if none is assigned. Levels can override each gate's maths per entry.
    /// Coins never go below 0, and only this run's coins change (coins saved earlier are safe).
    /// </summary>
    public class CoinGate : TrackHurdle
    {
        public enum Operation
        {
            Add,
            Subtract,
            Multiply,
            Divide
        }

        [Serializable]
        public struct Setting
        {
            public Operation operation;
            [Min(0)] public int amount;
        }

        [SerializeField] private Operation operation = Operation.Add;
        [SerializeField, Min(0)] private int amount = 10;

        [Header("Label")]
        [Tooltip("Optional. Left empty, a label is created in front of the gate.")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private float labelHeight = 0.3f;
        [SerializeField, Min(0.1f)] private float labelFontSize = 14f;
        [SerializeField] private Color labelColor = Color.white;

        public Operation CurrentOperation => operation;
        public int Amount => amount;

        /// <summary>Add and multiply help; subtract and divide hurt (x1 / ÷1 / +0 / -0 count as neutral-good).</summary>
        public bool IsGood => operation == Operation.Add || operation == Operation.Multiply ||
                              amount <= (operation == Operation.Divide ? 1 : 0);

        protected override void Awake()
        {
            base.Awake();
            EnsureLabel();
            Refresh();
        }

        public void Configure(Setting setting)
        {
            operation = setting.operation;
            amount = setting.amount;
            Refresh();
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var score = player.Score;
            if (score == null)
                return;

            var before = score.Coins;
            score.AddCoins(Apply(before) - before, true);
        }

        /// <summary>What 'coins' becomes after this gate.</summary>
        public int Apply(int coins)
        {
            switch (operation)
            {
                case Operation.Add: return coins + amount;
                case Operation.Subtract: return Mathf.Max(0, coins - amount);
                case Operation.Multiply: return coins * amount;
                case Operation.Divide: return amount > 0 ? coins / amount : coins;
                default: return coins;
            }
        }

        public string Text
        {
            get
            {
                switch (operation)
                {
                    case Operation.Add: return "+" + amount;
                    case Operation.Subtract: return "-" + amount;
                    case Operation.Multiply: return "x" + amount;
                    case Operation.Divide: return "÷" + amount;
                    default: return amount.ToString();
                }
            }
        }

        private void Refresh()
        {
            SetChoiceMatch(IsGood);
            if (label != null)
                label.text = Text;
        }

        private void EnsureLabel()
        {
            if (label != null)
                return;

            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            // A little in front of the gate (the player runs along +Z, so she sees the -Z side).
            go.transform.localPosition = new Vector3(0f, labelHeight, -0.3f);

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = labelFontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = labelColor;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = 10;
            label = text;
        }

        private void OnValidate()
        {
            if (label != null)
                label.text = Text;
        }
    }
}
