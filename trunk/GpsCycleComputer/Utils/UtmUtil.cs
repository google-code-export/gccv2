#region Using directives

using System;
using System.Runtime.InteropServices;

#endregion

namespace GpsUtils
{
    // convert lat/long into UTM system, so the distance can
    // be computed in km
    public class UtmUtil
    {
        public UtmUtil() { }

        // reference point
        private double referenceX = 0.0;
        private double referenceY = 0.0;
        private int referenceZone = 0;

        public double lat2meter = 0.0;
        public double longit2meter = 0.0;
        public double meter2lat = 0.0;
        public double meter2longit = 0.0;

        public bool referenceSet = false;

        // set the reference point (to compute distance from...)
        public void setReferencePoint(double lat, double longit)
        {
            ConvertToUtm(lat, longit, -1, 
                         out referenceX, out referenceY, out referenceZone);
            getXY(lat + 0.001, longit + 0.001, out longit2meter, out lat2meter);
            lat2meter *= 1000;
            longit2meter *= 1000;
            meter2lat = 1 / lat2meter;
            meter2longit = 1 / longit2meter;

            /*getLatLong(100, 100, out meter2lat, out meter2longit);
            meter2longit = (meter2longit - longit) / 100;
            meter2lat = (meter2lat - lat) / 100;
            longit2meter = 1 / meter2longit;
            lat2meter = 1 / meter2lat;
            */
            referenceSet = true;
        }

        // get x/y in metres relative to reference
        public void getXY(double lat, double longit, out double x, out double y)
        {
            double newX;
            double newY;
            int newZone;
            ConvertToUtm(lat, longit, referenceZone, out newX, out newY, out newZone);

            x = newX - referenceX;
            y = newY - referenceY;
        }

        // locate zone from lat and longit
        public static int determineZone(double lat, double longit)
        {
            int resulting_zone = (int)((longit + 180) / 6) + 1;

            // special zone for south Norway
            if ((lat >= 56) && (lat < 64) && (longit >= 3) && (longit < 12))
                { resulting_zone = 32; }

            // special zones for Svalbard
            if ((lat >= 72) && (lat < 84))
            {
                if (longit >= 0 && longit < 9)
                    { resulting_zone = 31; }
                else if	(longit >= 9 && longit < 21)
                    { resulting_zone = 33; }
                else if	(longit >= 21 && longit < 33)
                    { resulting_zone = 35; }
                else if	(longit >= 33 && longit < 42)
                    { resulting_zone = 37; }
            }
            return resulting_zone;
        }

        public static double degToRad(double x) { return x * Math.PI / 180.0; }
        public static double radToDeg(double x) { return x / Math.PI * 180.0; }
        public static double square(double x) { return x * x; }

        // compute UTM coordinates. If zone_in >= 0, then use that zone
        // A zone input is required to compute distance between two points
        // Note - this work correcly if zone are not VERY different (then
        // it need some Longitude adjustments) - but it should not happen in this app.
        public static void ConvertToUtm(double lat, double longit, int zone_in,
                                        out double UtmX, out double UtmY, 
                                        out int UtmZone)
        {
            // some consts
            double RADIUS_OF_EARTH = 6378137;
            // longitude offset
            double COORD_LONGITUDE_OFFSET = 500000;
            // flattening for WGS84
            double COORD_FLATTENING = (1 / 298.257225363);
            // eccentricity squared
            double COORD_ECC_SQUARED = (2 * COORD_FLATTENING - square(COORD_FLATTENING));
            // eccentricity squared to the 2
            double COORD_ECC_SQUARED2 = (square(COORD_ECC_SQUARED));
            // eccentricity squared to the 3
            double COORD_ECC_SQUARED3 = (square(COORD_ECC_SQUARED) * COORD_ECC_SQUARED);
            // eccentricity prime squared
            double COORD_ECC_PRIME_SQUARED = (COORD_ECC_SQUARED) / (1 - COORD_ECC_SQUARED);
            // constant
            double COORD_K0 = 0.9996;
            // constant
            double COORD_e1 = (1 - Math.Sqrt(1 - COORD_ECC_SQUARED)) / (1 + Math.Sqrt(1 - COORD_ECC_SQUARED));

            if (zone_in >= 0) { UtmZone = zone_in; }
            else { UtmZone = determineZone(lat, longit); }

            // converts lat/long to UTM coords, equations from USGS Bulletin 1532
            // east Longitudes are positive, west longitudes are negative.
            // north latitudes are positive, south latitudes are negative
            // lat and lon are in decimal degrees
            // written by Chuck Gantz- chuck.gantz@globalstar.com

            double LatRad = degToRad(lat);
            double LongRad = degToRad(longit);

            // +3 puts origin in middle of zone
            double  LongOrigin = ((double)UtmZone - 1) * 6 - 180 + 3;
            double  LongOriginRad = degToRad(LongOrigin);

            double N = RADIUS_OF_EARTH / Math.Sqrt(1.0 - COORD_ECC_SQUARED * square(Math.Sin(LatRad)));
            double T = square(Math.Tan(LatRad));
            double C = COORD_ECC_PRIME_SQUARED * square(Math.Cos(LatRad));
            double A = Math.Cos(LatRad) * (LongRad - LongOriginRad);
            double  A_POWER_2 = square(A);
            double  A_POWER_3 = A_POWER_2*A;

            double  M = RADIUS_OF_EARTH * ((1.0 - COORD_ECC_SQUARED / 4 - 3.0 * COORD_ECC_SQUARED2 / 64
                - 5.0 * COORD_ECC_SQUARED3 / 256) * LatRad - (3.0 * COORD_ECC_SQUARED / 8
                + 3.0 * COORD_ECC_SQUARED2 / 32 + 45.0 * COORD_ECC_SQUARED3 / 1024) * Math.Sin(2 * LatRad)
                + (15.0 * COORD_ECC_SQUARED2 / 256 + 45.0 * COORD_ECC_SQUARED3 / 1024)
                * Math.Sin(4 * LatRad) - (35.0 * COORD_ECC_SQUARED3 / 3072) * Math.Sin(6 * LatRad));

            UtmX = (COORD_K0 * N * (A + (1 - T + C) * A_POWER_3/*A**3*/ / 6
                + (5 - 18 * T + T * T + 72 * C - 58.0 * COORD_ECC_PRIME_SQUARED)
                * A_POWER_3 * A_POWER_2/*A**5*/ / 120) + COORD_LONGITUDE_OFFSET);

            UtmY = (COORD_K0 * (M + N * Math.Tan(LatRad) * (A_POWER_2/*A**2*/ / 2 + (5 - T + 9 * C
                + 4 * C * C) * square(A_POWER_2)/*A**4*/ / 24
                + (61 - 58 * T + square(T) + 600 * C
                    - 330.0 * COORD_ECC_PRIME_SQUARED) * square(A_POWER_3)/*A**6*/ / 720)));
        }

        // get lat/long relative to reference (to write back into KML file
        public void getLatLong(double x, double y, out double lat, out double longit)
        {
            double lat_out;
            double longit_out;

            ConvertToWorld(referenceX + x, referenceY + y, referenceZone, out lat_out, out longit_out);

            lat = lat_out;
            longit = longit_out;
        }

        public void ConvertToWorld(double v_x, double v_y, int v_zone, out double lat_, out double lon_)
        {
            // converts UTM coords to lat/long, equations from USGS Bulletin 1532
            // east Longitudes are positive, west longitudes are negative.
            // north latitudes are positive, south latitudes are negative
            // lat and lon are in decimal degrees.
            // written by Chuck Gantz- chuck.gantz@globalstar.com

            // some consts
            double COORD_MAXIMUM_LONGITUDE = 180.0;
            double RADIUS_OF_EARTH = 6378137;
            // longitude offset
            double COORD_LONGITUDE_OFFSET = 500000;
            // flattening for WGS84
            double COORD_FLATTENING = (1 / 298.257225363);
            // eccentricity squared
            double COORD_ECC_SQUARED = (2 * COORD_FLATTENING - square(COORD_FLATTENING));
            // eccentricity squared to the 2
            double COORD_ECC_SQUARED2 = (square(COORD_ECC_SQUARED));
            // eccentricity squared to the 3
            double COORD_ECC_SQUARED3 = (square(COORD_ECC_SQUARED) * COORD_ECC_SQUARED);
            // eccentricity prime squared
            double COORD_ECC_PRIME_SQUARED = (COORD_ECC_SQUARED) / (1 - COORD_ECC_SQUARED);
            // constant
            double COORD_K0 = 0.9996;
            // constant
            double COORD_e1 = (1 - Math.Sqrt(1 - COORD_ECC_SQUARED)) / (1 + Math.Sqrt(1 - COORD_ECC_SQUARED));


            // remove LONGITUDE_OFFSET (500000 meters) for longitude
            double x = v_x - COORD_LONGITUDE_OFFSET;
            // use internal y value
            double y = v_y;

            // +3 puts origin in middle of zone
            double LongOrigin = ((double)v_zone - 1) * 6 - 180 + 3;

            double M = y / COORD_K0;
            double mu = M / (RADIUS_OF_EARTH * (1.0 - COORD_ECC_SQUARED / 4.0
                - 3.0 * COORD_ECC_SQUARED2 / 64 - 5.0 * COORD_ECC_SQUARED3 / 256));

            double phi1Rad = mu + (3.0 * COORD_e1 / 2 - 27.0 * square(COORD_e1) * COORD_e1 / 32) * Math.Sin(2 * mu)
                + (21.0 * square(COORD_e1) / 16 - 55.0 * square(square(COORD_e1)) / 32) * Math.Sin(4 * mu)
                + (151.0 * square(COORD_e1) * COORD_e1 / 96) * Math.Sin(6 * mu);

            double N1 = RADIUS_OF_EARTH / Math.Sqrt(1.0 - COORD_ECC_SQUARED * square(Math.Sin(phi1Rad)));
            double T1 = square(Math.Tan(phi1Rad));
            double C1 = COORD_ECC_PRIME_SQUARED * square(Math.Cos(phi1Rad));
            double R1 = RADIUS_OF_EARTH * (1.0 - COORD_ECC_SQUARED)
                / Math.Exp(Math.Log(1.0 - COORD_ECC_SQUARED * square(Math.Sin(phi1Rad))) * 1.5);
            double D = x / (N1 * COORD_K0);
            double D_POWER_2 = square(D);
            double D_POWER_3 = D_POWER_2*D;

            lat_ = phi1Rad - (N1 * Math.Tan(phi1Rad) / R1) * (D_POWER_2/*D**2*/ / 2
                - (5 + 3 * T1 + 10 * C1 - 4 * square(C1) - 9.0 * COORD_ECC_PRIME_SQUARED)
                    * square(D_POWER_2)/*D**4*/ / 24
                + (61 + 90 * T1 + 298 * C1 + 45 * square(T1) - 252.0 * COORD_ECC_PRIME_SQUARED - 3 * square(C1))
                    * square(D_POWER_3)/*D**6*/ / 720);
            lat_ = radToDeg(lat_);

            lon_ = (D - (1 + 2 * T1 + C1) * D_POWER_3/*D**3*/ / 6
                + (5 - 2 * C1 + 28 * T1 - 3 * square(C1) + 8.0 * COORD_ECC_PRIME_SQUARED
                + 24 * square(T1)) * D_POWER_2 * D_POWER_3/*D**5*/ / 120) / Math.Cos(phi1Rad);
            lon_ = LongOrigin + radToDeg(lon_);

            // correction of longitude value
            if (lon_ > COORD_MAXIMUM_LONGITUDE)
            {
                lon_ -= 360;
            }
            else if (lon_ < -COORD_MAXIMUM_LONGITUDE)
            {
                lon_ += 360;
            }
        }

    }
}
