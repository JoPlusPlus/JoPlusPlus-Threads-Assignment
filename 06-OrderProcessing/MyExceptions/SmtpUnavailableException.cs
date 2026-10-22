namespace Mini_Project___Order_Processing_System.MyExceptions;

public class SmtpUnavailableException : Exception
{
    public SmtpUnavailableException(string message) : base(message)
    {
    }
}
