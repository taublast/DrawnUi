namespace DrawnUi.Infrastructure;

public class PerpetualPendulum : Pendulum
{
    public override void Update(double timeStep)
    {
        //Angle of wire relative to y-axis
        /*
		 *   \\\\\\\\\\\\\\\\
		 *         |\
		 *         | \
		 *         |  \
		 *         |   \
		 *         | O  \
		 *         |     \
		 * 
		 * To the right is positive, and to the left is negative
		 * Unit: Radians
		 */

        angle = wireVector.getAngle() - 3.0 * Math.PI / 2.0;

        //Perpetual 
        accelerationVector.setMagnitude(Gravity * Math.Sin(angle));
        if ((wireVector.getAngle() % (2 * Math.PI)) - (((3.0 * Math.PI) / 2.0)) > 0)
            accelerationVector.setAngle(wireVector.getAngle() - (Math.PI / 2.0));
        else
            accelerationVector.setAngle(angle);

        AngularVelocity += angularAcceleration * timeStep;
        wireVector.setAngle(wireVector.getAngle() + AngularVelocity * timeStep);

        velocityVector.setMagnitude(AngularVelocity);
        if (AngularVelocity < 0)
            velocityVector.setAngle(wireVector.getAngle() - Math.PI / 2.0);
        else
            velocityVector.setAngle(angle);

    }
}

public class Pendulum
{
    protected Vector wireVector;
    protected Vector accelerationVector;
    protected Vector velocityVector;

    protected double angularAcceleration;
    public double AngularVelocity { get; protected set; }

    public double RodLength
    {
        get
        {
            return _rodLength;
        }

        set
        {
            if (_rodLength != value)
            {
                _rodLength = value;
                CreateVector();
            }
        }
    }
    double _rodLength = 100;

    public double AirResistance { get; set; }

    public double Gravity { get; set; }

    protected double angle;

    private double initialAngle, initialVelocity;


    void CreateVector()
    {
        wireVector = new Vector(1, RodLength);
    }
    public Pendulum()
    {
        CreateVector();

        accelerationVector = new Vector(0, 0);

        velocityVector = new Vector(0, 0);
    }

    public virtual void Update(double timeStep)
    {
        //Angle of wire relative to y-axis
        /*
		 *   \\\\\\\\\\\\\\\\
		 *         |\
		 *         | \
		 *         |  \
		 *         |   \
		 *         | O  \
		 *         |     \
		 * 
		 * To the right is positive, and to the left is negative
		 * Unit: Radians
		 */

        angle = wireVector.getAngle() - 3.0 * Math.PI / 2.0;

        angularAcceleration = -(AirResistance * AngularVelocity) - Gravity * Math.Sin(angle);
        accelerationVector.setMagnitude(angularAcceleration);
        if (angularAcceleration < 0)
            accelerationVector.setAngle(wireVector.getAngle() - Math.PI / 2.0);
        else
            accelerationVector.setAngle(angle);


        AngularVelocity += angularAcceleration * timeStep;
        wireVector.setAngle(wireVector.getAngle() + AngularVelocity * timeStep);

        velocityVector.setMagnitude(AngularVelocity);
        if (AngularVelocity < 0)
            velocityVector.setAngle(wireVector.getAngle() - Math.PI / 2.0);
        else
            velocityVector.setAngle(angle);

        //		System.out.println("------");
        //		System.out.println(angularAcceleration);
        //		System.out.println(angularVelocity);
        //		System.out.println(angle);
    }

    private double accel;

    public void Reset()
    {
        accel = 0.1;
        wireVector.setAngle(3.0 * Math.PI / 2.0 + initialAngle);
        AngularVelocity = initialVelocity;
    }

    public void SetAmplitude(double value) { RodLength = value; }

    public double getInitialAngle() { return initialAngle; }
    public void setInitialAngle(double initialAngle) { this.initialAngle = initialAngle; }

    public double getInitialVelocity() { return initialVelocity; }
    public void setInitialVelocity(double initialVelocity) { this.initialVelocity = initialVelocity; }

    public double getAngle() { return angle; }



    public double getAirResistence() { return AirResistance; }
    public void setAirResistance(double airResistence) { AirResistance = airResistence; }

    public Vector getWireVector() { return wireVector; }
    public Vector getAccelerationVector() { return accelerationVector; }
    public Vector getVelocityVector() { return velocityVector; }

    public double getAngularAcceleration() { return angularAcceleration; }
    public double getAngularVelocity() { return AngularVelocity; }


}