namespace SignalRed.Common.Messages
{
    public class ScreenMessage
    {
        public string NewScreen { get; set; }

        public ScreenMessage() { }

        public ScreenMessage(string newScreen)
        {
            NewScreen = newScreen;
        }
    }
}
