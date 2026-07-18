using UnityEngine;

public class CoordinateConverter : MonoBehaviour
{
    private double originLatitude;
    private double originLongitude;

    [SerializeField]
    private float scale = 1000f;

    public void SetOrigin(double latitude, double longitude)
    {
        originLatitude = latitude;
        originLongitude = longitude;
    }
    public Vector3 ToUnityPosition(double latitude, double longitude)
    {
        double deltaLatitude=latitude-originLatitude;
        double deltaLongitude=longitude-originLongitude;
        double x = deltaLongitude * scale, y = 0, z = deltaLatitude * scale;
        Vector3 point = new Vector3((float)x, (float)y, (float)z);
        return point;
    }
}