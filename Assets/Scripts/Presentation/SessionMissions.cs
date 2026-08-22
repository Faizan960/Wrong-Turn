using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Session missions (Phase 5 Part 7): one bite-sized goal at a time on the
    /// game-over screen — "MISSION: REACH COMBO 15". Completing it flashes green
    /// and advances to the next. In-memory only, like RunsThisSession: the
    /// ladder deliberately resets each launch so every session starts with an
    /// easy win. Watches the event bus; owns no gameplay.
    /// </summary>
    public class SessionMissions : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color doneColor = new Color32(0, 255, 136, 255);
        [SerializeField] private Color pendingColor = new Color(1f, 1f, 1f, 0.35f);

        private enum Kind { ReachCombo, CorrectInRun, SurviveChaos, ScoreInRun, PlayRuns }

        private struct Mission
        {
            public Kind Kind;
            public int Target;
            public string Text;

            public Mission(Kind kind, int target, string text)
            {
                Kind = kind;
                Target = target;
                Text = text;
            }
        }

        private static readonly Mission[] Ladder =
        {
            new Mission(Kind.ScoreInRun, 5, "GET 5 SCORE"),
            new Mission(Kind.ReachCombo, 5, "REACH COMBO 5"),
            new Mission(Kind.ScoreInRun, 15, "GET 15 SCORE"),
            new Mission(Kind.SurviveChaos, 1, "SURVIVE A CHAOS EVENT"),
            new Mission(Kind.ReachCombo, 15, "REACH COMBO 15"),
            new Mission(Kind.ScoreInRun, 30, "GET 30 SCORE"),
            new Mission(Kind.PlayRuns, 5, "PLAY 5 GAMES"),
            new Mission(Kind.ScoreInRun, 50, "GET 50 SCORE"),
            new Mission(Kind.ReachCombo, 30, "REACH COMBO 30"),
            new Mission(Kind.CorrectInRun, 50, "GET 50 CORRECT IN ONE RUN"),
            new Mission(Kind.ReachCombo, 50, "REACH COMBO 50"),
            new Mission(Kind.ReachCombo, 100, "REACH COMBO 100"),
        };

        private int _index;
        private bool _doneThisRun;
        private int _bestComboThisRun;
        private int _correctThisRun;
        private int _scoreThisRun;
        private int _runsThisSession;
        private bool _chaosStartedThisRun;
        private bool _survivedChaosThisRun;
        private Tween _pop;

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnComboChanged += HandleCombo;
            GameEvents.OnScoreChanged += HandleScore;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnChaosStarted += HandleChaosStarted;
            GameEvents.OnChaosEnded += HandleChaosEnded;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnComboChanged -= HandleCombo;
            GameEvents.OnScoreChanged -= HandleScore;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnChaosStarted -= HandleChaosStarted;
            GameEvents.OnChaosEnded -= HandleChaosEnded;
            GameEvents.OnRunEnded -= HandleRunEnded;
            _pop?.Kill();
        }

        private void HandleRunStarted()
        {
            _doneThisRun = false;
            _bestComboThisRun = 0;
            _correctThisRun = 0;
            _scoreThisRun = 0;
            _chaosStartedThisRun = false;
            _survivedChaosThisRun = false;
        }

        private void HandleCombo(int combo) => _bestComboThisRun = Mathf.Max(_bestComboThisRun, combo);

        private void HandleScore(int score, int delta) => _scoreThisRun = Mathf.Max(_scoreThisRun, score);

        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (correct) _correctThisRun++;
        }

        private void HandleChaosStarted(ChaosEffect effect) => _chaosStartedThisRun = true;

        private void HandleChaosEnded(ChaosType type) => _survivedChaosThisRun = _chaosStartedThisRun;

        private void HandleRunEnded(RunResult result)
        {
            _runsThisSession++;
            if (label == null) return;
            if (_index >= Ladder.Length)
            {
                label.text = string.Empty; // ladder cleared this session
                return;
            }

            var mission = Ladder[_index];
            _doneThisRun =
                (mission.Kind == Kind.ReachCombo && _bestComboThisRun >= mission.Target) ||
                (mission.Kind == Kind.CorrectInRun && _correctThisRun >= mission.Target) ||
                (mission.Kind == Kind.SurviveChaos && _survivedChaosThisRun) ||
                (mission.Kind == Kind.ScoreInRun && _scoreThisRun >= mission.Target) ||
                (mission.Kind == Kind.PlayRuns && _runsThisSession >= mission.Target);

            if (_doneThisRun)
            {
                label.color = doneColor;
                label.text = $"MISSION COMPLETE  {mission.Text}";
                _pop?.Kill();
                label.rectTransform.localScale = Vector3.one * 1.25f;
                _pop = label.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
                _index++;
            }
            else
            {
                label.color = pendingColor;
                label.text = $"MISSION  {mission.Text}";
            }
        }
    }
}
