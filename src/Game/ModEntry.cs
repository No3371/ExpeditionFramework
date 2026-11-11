namespace ExpeditionFramework.Game
{
    public class ModEntry : IMod
    {
        public const string ModId = "expedition_framework";

        internal static ModLog Log;

        internal static string L(string key, params object[] args) => ModHelper.GetLocalized(ModId, key, args);

        public void Init(ModManifest manifest)
        {
            Log = new ModLog(manifest);
        }

        public void OnEnable()
        {
            // Mods enable in folder order and prerequisites don't reorder them, so packs are pulled in from here.
            PackRegistry.Start();
            ModHook.OnModAssetsLoaded += PackRegistry.Register;
            HandsWindow.Start();
        }

        public void OnDisable()
        {
            ModHook.OnModAssetsLoaded -= PackRegistry.Register;
            HandsWindow.Stop();
            PackRegistry.Stop();
        }
    }
}
