using AppoMobi.Specials;
using DrawnUi.Controls;

namespace DrawnUi.Camera;

public class CameraUnit : IHasStringId
{
    public Metadata Meta { get; set; }

    public float FieldOfView { get; set; }

    public CameraPosition Facing { get; set; }

    public string Id { get; set; }

    public float PixelXDimension { get; set; }
    public float PixelYDimension { get; set; }

    public List<float> FocalLengths { get; set; }

    public float MinFocalDistance { get; set; }
    public float LensDistortion { get; set; }
    public float SensorWidth { get; set; }
    public float SensorHeight { get; set; }

    private float _FocalLength;
    public float FocalLength
    {
        get { return _FocalLength; }
        set
        {
            if (_FocalLength != value)
            {
                _FocalLength = value;
                //OnPropertyChanged();
            }
        }
    }

    public double FocalLength35mm
    {
        get
        {
            return FocalLength * SensorCropFactor;
        }
    }




    /// <summary>
    /// In millimeters!
    /// </summary>
    public double SensorCropFactor
    {
        get
        {
            if (SensorHeight > 0 && SensorWidth > 0)
            {
                if (_sensorCropFactor == 0)
                {
                    _sensorCropFactor = Calculate.CropFactor(SensorWidth, SensorHeight);
                }
            }
            return _sensorCropFactor;
        }
        set
        {
            _sensorCropFactor = value;
        }
    }
    private double _sensorCropFactor;



}