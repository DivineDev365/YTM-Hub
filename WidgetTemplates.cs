namespace YTMHub;

public static class WidgetTemplates
{
    public static string GetTemplate()
    {
        return @"
{
    ""type"": ""AdaptiveCard"",
    ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
    ""version"": ""1.5"",
    ""body"": [
        {
            ""type"": ""Container"",
            ""items"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""${if(isRunning, 'YouTube Music', 'Launch YTMHub to play music')}"",
                    ""weight"": ""Bolder"",
                    ""size"": ""Medium"",
                    ""wrap"": true,
                    ""horizontalAlignment"": ""Center""
                },
                {
                    ""type"": ""ActionSet"",
                    ""isVisible"": ${isRunning},
                    ""horizontalAlignment"": ""Center"",
                    ""actions"": [
                        {
                            ""type"": ""Action.Execute"",
                            ""title"": ""⏮"",
                            ""verb"": ""previous""
                        },
                        {
                            ""type"": ""Action.Execute"",
                            ""title"": ""${if(isPlaying, '⏸', '▶️')}"",
                            ""verb"": ""playpause""
                        },
                        {
                            ""type"": ""Action.Execute"",
                            ""title"": ""⏭"",
                            ""verb"": ""next""
                        }
                    ]
                }
            ]
        }
    ]
}
";
    }

    public static string GetData(bool isRunning, bool isPlaying)
    {
        return $@"
{{
    ""isRunning"": {isRunning.ToString().ToLower()},
    ""isPlaying"": {isPlaying.ToString().ToLower()}
}}
";
    }
}
