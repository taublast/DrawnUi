namespace DrawnUi.Infrastructure;

public struct Spring
{
    public float Mass { get; set; }
    public float Stiffness { get; set; }
    public float DampingRatio { get; set; }

    public Spring(float mass, float stiffness, float dampingRatio)
    {
        Mass = mass;
        Stiffness = stiffness;
        DampingRatio = dampingRatio;
    }

    public static Spring Damped => new Spring(1, 200, 1f);

    public static Spring Default => new Spring(1, 200, 0.5f);
}