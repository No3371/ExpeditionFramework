namespace ExpeditionFramework.Core
{
    /// <summary>
    /// The pack the in-game self-test plays told intel on. It is compiled only when the self-test runs and never added to the
    /// map, so it needs no text or art. Every slot's dice are fixed, so each check has one right answer: street's rumour can
    /// tell two cards a day, cellar's whisper six and only once street is cleared, and yard is at its intel_max as soon as
    /// its rolled notice shows.
    /// </summary>
    internal static class SelfTestPack
    {
        public const string Id = "ef_selftest";

        public const string Json = @"{
  ""rooms"": {
    ""r1"": { ""text"": ""k"" }, ""r2"": { ""text"": ""k"" }, ""r3"": { ""text"": ""k"" }, ""r4"": { ""text"": ""k"" },
    ""r5"": { ""text"": ""k"" }, ""r6"": { ""text"": ""k"" }, ""r7"": { ""text"": ""k"" }
  },
  ""areas"": {
    ""street"": {
      ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r1"" } ],
      ""pools"": { ""leads"": [ ""r1"", ""r2"", ""r3"" ] },
      ""intel"": [
        { ""id"": ""sign"", ""pool"": ""leads"", ""chance"": 100, ""title"": ""k"", ""description"": ""k"" },
        { ""id"": ""rumour"", ""pool"": ""leads"", ""told"": true, ""counts"": [ 0, 0, 100 ], ""title"": ""k"", ""description"": ""k"" }
      ]
    },
    ""cellar"": {
      ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r1"" } ], ""unlocked_by"": ""street"",
      ""pools"": { ""whispers"": [ ""r1"", ""r2"", ""r3"", ""r4"", ""r5"", ""r6"", ""r7"" ] },
      ""intel"": [
        { ""id"": ""whisper"", ""pool"": ""whispers"", ""told"": true, ""counts"": [ 0, 0, 0, 0, 0, 0, 100 ], ""title"": ""k"", ""description"": ""k"" }
      ]
    },
    ""yard"": {
      ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r1"" } ], ""intel_max"": 1,
      ""pools"": { ""leads"": [ ""r1"", ""r2"" ] },
      ""intel"": [
        { ""id"": ""notice"", ""pool"": ""leads"", ""chance"": 100, ""title"": ""k"", ""description"": ""k"" },
        { ""id"": ""talk"", ""pool"": ""leads"", ""told"": true, ""chance"": 100, ""title"": ""k"", ""description"": ""k"" }
      ]
    }
  }
}";
    }
}
