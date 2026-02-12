// Visualiser adds new functions which are called from game in here


public static class MqttApi
{
    // Example invocation: MqttApi.BuzzSuccess() -> can see in DummyPublisher.cs
    public static void BuzzSuccess()
    {
        MqttService.Instance.PublishHeeHeeHorHor(
            MqttService.PublishTopic.HEEHEEHORHOR_TOPIC,
            new Heeheehorhor("HEEHEEHORHOR_SUCCESS")
        );
    }
}
