namespace DrawnUi.Infrastructure;

public class Vector
{
    public static double Radians(double degrees)
    {
        return degrees * 0.0174533;
    }

    private double X, Y;
    private double magnitude, angle;

    public Vector()
    {

    }

    public Vector(double x1, double y1, double x2, double y2)
    {
        X = x2 - x1;
        Y = y2 - y1;

        calculateMagnitude();

        X /= magnitude;
        Y /= magnitude;

        magnitude = 1;
        calculateAngle();
    }

    public Vector(double xComp, double yComp)
    {

        this.X = xComp;
        this.Y = yComp;
        calculateMagnitude();
        calculateAngle();
    }

    public void setComponents(double xComp, double yComp)
    {
        this.X = xComp;
        this.Y = yComp;

        calculateMagnitude();
        calculateAngle();
    }

    public static double CrossProduct(Vector a, Vector b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public double Cross(Vector b)
    {
        return this.X * b.Y - this.Y * b.X;
    }

    public double getXComp()
    {
        return X;
    }

    public void setXComp(double xComp)
    {
        this.X = xComp;

        calculateMagnitude();
        calculateAngle();
    }

    public double getYComp()
    {
        return Y;
    }

    public void setYComp(double yComp)
    {
        this.Y = yComp;

        calculateMagnitude();
        calculateAngle();
    }

    public Vector add(Vector other)
    {
        return new Vector(X + other.X,
            Y + other.Y);
    }

    public void mAdd(Vector other)
    {
        setComponents(X + other.X, Y + other.Y);
    }

    public void setAngle(double angle)
    {
        //		if(angle < 0) 
        //			angle += 2.0 * Math.PI;
        //		if(angle > 2.0 * Math.PI)
        //			angle -= 2.0 * Math.PI;

        this.angle = angle;

        X = magnitude * Math.Cos(angle);
        Y = magnitude * Math.Sin(angle);
    }

    public void setMagnitude(double magnitude)
    {
        this.magnitude = Math.Abs(magnitude);
        X = magnitude * Math.Cos(angle);
        Y = magnitude * Math.Sin(angle);

        if (magnitude < 0)
            setAngle(angle - Math.PI);
    }

    public double getMagnitude()
    {
        return magnitude;
    }

    private void calculateMagnitude()
    {
        magnitude = Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));
    }

    public void mScale(double scalar)
    {
        setMagnitude(magnitude * scalar);
    }

    public Vector scale(double scalar)
    {
        return new Vector(X * scalar, Y * scalar);
    }

    public double getAngle()
    {
        return angle;
    }

    private void calculateAngle()
    {
        angle = Math.Atan2(Y, X);

        //		if(this.angle < 0) 
        //			this.angle += 2.0 * Math.PI;
    }
}