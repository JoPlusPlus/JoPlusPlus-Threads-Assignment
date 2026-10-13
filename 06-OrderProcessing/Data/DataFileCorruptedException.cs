namespace Mini_Project___Order_Processing_System.Data;

public class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string fileName, Exception? inner = null)
        : base($"Data file '{fileName}' is corrupted (invalid JSON).", inner) { }
}
