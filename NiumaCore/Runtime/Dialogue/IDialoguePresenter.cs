namespace NiumaCore.Dialogue
{
    public interface IDialoguePresenter
    {
        void OpenDialogue(DialogueUnit unit);
        void PlayLine(DialogueLine line);
        void ShowChoices(DialogueChoice[] choices);
        void CloseDialogue();
    }
}
