namespace EfStarter
{
    // The game lists only mods that ship a DLL with an IMod class. Everything else in this pack is data for the
    // Expedition Framework, so the class does nothing. IMod and ModManifest come from the game's Assembly-CSharp.
    public class ModEntry : IMod
    {
        public void Init(ModManifest manifest) { }
        public void OnEnable() { }
        public void OnDisable() { }
    }
}
