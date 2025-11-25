namespace LogGrid.Client
{
    public sealed class EffectiveLogLevels
    {
        public bool Debug { get; init; }
        public bool Information { get; init; }
        public bool Warning { get; init; }
        public bool Error { get; init; }
    }

    public static class LogLevelEvaluator
    {
        public static EffectiveLogLevels Evaluate(DirectLogLevels? configuredLevels)
        {
            var levels = configuredLevels ?? new DirectLogLevels();

            var errorEnabled = levels.Error;
            var warningEnabled = levels.Warning && errorEnabled;
            var infoEnabled = levels.Info && warningEnabled;
            var debugEnabled = levels.Debug && infoEnabled;

            return new EffectiveLogLevels
            {
                Error = errorEnabled,
                Warning = warningEnabled,
                Information = infoEnabled,
                Debug = debugEnabled
            };
        }
    }
}

