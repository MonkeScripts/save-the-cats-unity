// This file has all the payload formats so that we can easily serialize and deserialize

[System.Serializable] // Required for JsonUtility
public class Heeheehorhor
{
    public string msg;
    public Heeheehorhor(string msg)
    {
        this.msg = msg;
    }
}