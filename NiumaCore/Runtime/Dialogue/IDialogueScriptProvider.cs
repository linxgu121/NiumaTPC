namespace NiumaCore.Dialogue
{
    public interface IDialogueScriptProvider
    {
        DialogueUnit Load(string dialogueId);
    }
}
