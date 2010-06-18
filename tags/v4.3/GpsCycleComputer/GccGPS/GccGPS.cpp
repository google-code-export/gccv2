#include "stdafx.h"
#include <service.h>   // for IOCTL flags
#include <string>
#include <vector>
#include "GccGPS.h"

//#define WRITE_LINES_TO_FILE		// debug write into file - uncomment, if required

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

//-----------------------------------------------------------------------------
// Microsoft generate code to init DLL

// CGccGPSApp
BEGIN_MESSAGE_MAP(CGccGPSApp, CWinApp)
END_MESSAGE_MAP()

// CGccGPSApp construction
CGccGPSApp::CGccGPSApp()
{
    // TODO: add construction code here,
    // Place all significant initialization in InitInstance
}

// The one and only CGccGPSApp object
CGccGPSApp theApp;

// CGccGPSApp initialization
BOOL CGccGPSApp::InitInstance()
{
    CWinApp::InitInstance();

    return TRUE;
}


// lock read/gps status functions, to avoid over-flooding on timer calls
bool __read_lock = false;                  

//-----------------------------------------------------------------------------
// send IOCTL_SERVICE_START to the driver. Returns 0 (false) on errors.
// use to start service (but it does not turn the harware on)!
extern "C" __declspec(dllexport) int GccGpsStart()
{
    if(__read_lock) { return 0; }

    HANDLE hGPS = CreateFile(L"GPD0:",				// pointer to name of the file
                             GENERIC_READ,			// access (read-write) mode
                             FILE_SHARE_READ | FILE_SHARE_WRITE,// share mode
                             NULL,				// pointer to security descriptor (not used)
                             OPEN_EXISTING,			// how to create (must exist)
                             FILE_ATTRIBUTE_NORMAL,		// file attributes
                             NULL);				// handle to file with attributes to copy (not used)

    if (hGPS != INVALID_HANDLE_VALUE)
    {
        __read_lock = true;
        BOOL ret = DeviceIoControl(hGPS,	        // Handle to the device, opened with CreateFile
                                   IOCTL_SERVICE_START, // DWORD=UINT32 : IOCTL for the operation
                                   NULL,		// LPVOID pointer to input buffer, if used
                                   0,			// DWORD nInBufferSize
                                   NULL,		// LPVOID pointer to output buffer, if used
                                   0,			// DWORD nOutBufferSize
                                   NULL,		// LPDWORD lpBytesReturned  : pointer to bytes received
                                   NULL);		// Not used, set to NULL
        CloseHandle(hGPS);
        __read_lock = false;

        // return 0 by DeviceIoControl means failure
        if(ret == 0)
            { return 0; }
    }
    else
        { return 0; }

    return 1;
}
//-----------------------------------------------------------------------------
// send IOCTL_SERVICE_STOP to the driver. Returns 0 (false) on errors.
// use to stop service completely. Might not be used at all, as GPS does turn off automatically to save power
extern "C" __declspec(dllexport) int GccGpsStop()
{
    if(__read_lock) { return 0; }

    HANDLE hGPS = CreateFile(L"GPD0:",				  // pointer to name of the file
                             GENERIC_READ,			  // access (read-write) mode
                             FILE_SHARE_READ | FILE_SHARE_WRITE,  // share mode
                             NULL,				  // pointer to security descriptor (not used)
                             OPEN_EXISTING,			  // how to create (must exist)
                             FILE_ATTRIBUTE_NORMAL,		  // file attributes
                             NULL);				  // handle to file with attributes to copy (not used)

    if (hGPS != INVALID_HANDLE_VALUE)
    {
        __read_lock = true;
        BOOL ret = DeviceIoControl(hGPS,	       // Handle to the device, opened with CreateFile
                                   IOCTL_SERVICE_STOP, // DWORD=UINT32 : IOCTL for the operation
                                   NULL,	       // LPVOID pointer to input buffer, if used
                                   0,		       // DWORD nInBufferSize
                                   NULL,	       // LPVOID pointer to output buffer, if used
                                   0,		       // DWORD nOutBufferSize
                                   NULL,	       // LPDWORD lpBytesReturned  : pointer to bytes received
                                   NULL);	       // Not used, set to NULL
        CloseHandle(hGPS);
        __read_lock = false;

        // return 0 by DeviceIoControl means failure
        if(ret == 0)
            { return 0; }
    }
    else
        { return 0; }

    return 1;
}
//-----------------------------------------------------------------------------
// send IOCTL_SERVICE_REFRESH to the driver. Returns 0 (false) on errors.
// use to refresh config param if e.g. registry changes
extern "C" __declspec(dllexport) int GccGpsRefresh()
{
    if(__read_lock) { return 0; }

    HANDLE hGPS = CreateFile(L"GPD0:",				 // pointer to name of the file
                             GENERIC_READ,			 // access (read-write) mode
                             FILE_SHARE_READ | FILE_SHARE_WRITE, // share mode
                             NULL,				 // pointer to security descriptor (not used)
                             OPEN_EXISTING,			 // how to create (must exist)
                             FILE_ATTRIBUTE_NORMAL,		 // file attributes
                             NULL);				 // handle to file with attributes to copy (not used)

    if (hGPS != INVALID_HANDLE_VALUE)
    {
        __read_lock = true;
        BOOL ret = DeviceIoControl(hGPS,		  // Handle to the device, opened with CreateFile
                                   IOCTL_SERVICE_REFRESH, // DWORD=UINT32 : IOCTL for the operation
                                   NULL,		  // LPVOID pointer to input buffer, if used
                                   0,			  // DWORD nInBufferSize
                                   NULL,		  // LPVOID pointer to output buffer, if used
                                   0,			  // DWORD nOutBufferSize
                                   NULL,		  // LPDWORD lpBytesReturned  : pointer to bytes received
                                   NULL);		  // Not used, set to NULL
        CloseHandle(hGPS);
        __read_lock = false;

        // return 0 by DeviceIoControl means failure
        if(ret == 0)
            { return 0; }
    }
    else
        { return 0; }

    return 1;
}
//-----------------------------------------------------------------------------
// send IOCTL_SERVICE_STATUS to the driver. Returns 0 (false) on errors.
// return current service status (1 added to the value returned by DeviceIoControl:
//    1 	The service is turned off.
//    2 	The service is turned on.
//    3 	The service is in the process of starting up.
//    4 	The service is in the process of shutting down.
//    5 	The service is in the process of unloading.
//    6 	The service is not uninitialized.
//    7 	The state of the service is unknown.
extern "C" __declspec(dllexport) int GccGpsStatus()
{
    if(__read_lock) { return 0; }

    HANDLE hGPS = CreateFile(L"GPD0:",				 // pointer to name of the file
                             GENERIC_READ,			 // access (read-write) mode
                             FILE_SHARE_READ | FILE_SHARE_WRITE, // share mode
                             NULL,				 // pointer to security descriptor (not used)
                             OPEN_EXISTING,			 // how to create (must exist)
                             FILE_ATTRIBUTE_NORMAL,		 // file attributes
                             NULL);				 // handle to file with attributes to copy (not used)

    DWORD return_value = 0;
    DWORD return_size = 0;

    if (hGPS != INVALID_HANDLE_VALUE)
    {
        __read_lock = true;
        BOOL ret = DeviceIoControl(hGPS,		  // Handle to the device, opened with CreateFile
                                   IOCTL_SERVICE_STATUS,  // DWORD=UINT32 : IOCTL for the operation
                                   NULL,		  // LPVOID pointer to input buffer, if used
                                   0,			  // DWORD nInBufferSize
                                   &return_value,	  // LPVOID pointer to output buffer, if used
                                   sizeof(DWORD),	  // DWORD nOutBufferSize
                                   &return_size,	  // LPDWORD lpBytesReturned  : pointer to bytes received
                                   NULL);		  // Not used, set to NULL
        CloseHandle(hGPS);
        __read_lock = false;

        // handle ff...ff case
        if(return_value == 0xffffffff)
            {return_value = 6; }

        // return 0 by DeviceIoControl means failure
        if(ret == 0)
            { return 0; }
    }
    else
        { return 0; }

    return (return_value + 1);
}
//-----------------------------------------------------------------------------

/* Some info about NMEA format from http://aprs.gids.nl/nmea

//-----------------------------------------------------------------------------
$GPGGA

Global Positioning System Fix Data

Name 	Example Data 	Description
Sentence Identifier 	$GPGGA 	Global Positioning System Fix Data
Time 	170834 	17:08:34 Z
Latitude 	4124.8963, N 	41d 24.8963' N or 41d 24' 54" N
Longitude 	08151.6838, W 	81d 51.6838' W or 81d 51' 41" W
Fix Quality:
- 0 = Invalid
- 1 = GPS fix
- 2 = DGPS fix 	1 	Data is from a GPS fix
Number of Satellites 	05 	5 Satellites are in view
Horizontal Dilution of Precision (HDOP) 	1.5 	Relative accuracy of horizontal position
Altitude 	280.2, M 	280.2 meters above mean sea level
Height of geoid above WGS84 ellipsoid 	-34.0, M 	-34.0 meters
Time since last DGPS update 	blank 	No last update
DGPS reference station id 	blank 	No station id
Checksum 	*75 	Used by program to check for transmission errors

Courtesy of Brian McClure, N8PQI.

Global Positioning System Fix Data. Time, position and fix related data for a GPS receiver.

eg2. $--GGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx

$GPGGA,170834,4124.8963,N,


hhmmss.ss = UTC of position
llll.ll = latitude of position
a = N or S
yyyyy.yy = Longitude of position
a = E or W
x = GPS Quality indicator (0=no fix, 1=GPS fix, 2=Dif. GPS fix)
xx = number of satellites in use
x.x = horizontal dilution of precision
x.x = Antenna altitude above mean-sea-level
M = units of antenna altitude, meters
x.x = Geoidal separation
M = units of geoidal separation, meters
x.x = Age of Differential GPS data (seconds)
xxxx = Differential reference station ID

eg3. $GPGGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx*hh
1    = UTC of Position
2    = Latitude
3    = N or S
4    = Longitude
5    = E or W
6    = GPS quality indicator (0=invalid; 1=GPS fix; 2=Diff. GPS fix)
7    = Number of satellites in use [not those in view]
8    = Horizontal dilution of position
9    = Antenna altitude above/below mean sea level (geoid)
10   = Meters  (Antenna height unit)
11   = Geoidal separation (Diff. between WGS-84 earth ellipsoid and
       mean sea level.  -=geoid is below WGS-84 ellipsoid)
12   = Meters  (Units of geoidal separation)
13   = Age in seconds since last update from diff. reference station
14   = Diff. reference station ID#
15   = Checksum


//-----------------------------------------------------------------------------
$GPRMC

Recommended minimum specific GPS/Transit data

eg1. $GPRMC,081836,A,3751.65,S,14507.36,E,000.0,360.0,130998,011.3,E*62
eg2. $GPRMC,225446,A,4916.45,N,12311.12,W,000.5,054.7,191194,020.3,E*68


           225446       Time of fix 22:54:46 UTC
           A            Navigation receiver warning A = OK, V = warning
           4916.45,N    Latitude 49 deg. 16.45 min North
           12311.12,W   Longitude 123 deg. 11.12 min West
           000.5        Speed over ground, Knots
           054.7        Course Made Good, True
           191194       Date of fix  19 November 1994
           020.3,E      Magnetic variation 20.3 deg East
           *68          mandatory checksum


eg3. $GPRMC,220516,A,5133.82,N,00042.24,W,173.8,231.8,130694,004.2,W*70
              1    2    3    4    5     6    7    8      9     10  11 12


      1   220516     Time Stamp
      2   A          validity - A-ok, V-invalid
      3   5133.82    current Latitude
      4   N          North/South
      5   00042.24   current Longitude
      6   W          East/West
      7   173.8      Speed in knots
      8   231.8      True course
      9   130694     Date Stamp
      10  004.2      Variation
      11  W          East/West
      12  *70        checksum


eg4. $GPRMC,hhmmss.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,ddmmyy,x.x,a*hh
1    = UTC of position fix
2    = Data status (V=navigation receiver warning)
3    = Latitude of fix
4    = N or S
5    = Longitude of fix
6    = E or W
7    = Speed over ground in knots
8    = Track made good in degrees True
9    = UT date
10   = Magnetic variation degrees (Easterly var. subtracts from true course)
11   = E or W
12   = Checksum

//-----------------------------------------------------------------------------
$GPGSA

GPS DOP and active satellites

eg1. $GPGSA,A,3,,,,,,16,18,,22,24,,,3.6,2.1,2.2*3C
eg2. $GPGSA,A,3,19,28,14,18,27,22,31,39,,,,,1.7,1.0,1.3*35


1    = Mode:
       M=Manual, forced to operate in 2D or 3D
       A=Automatic, 3D/2D
2    = Mode:
       1=Fix not available
       2=2D
       3=3D
3-14 = IDs of SVs used in position fix (null for unused fields)
15   = PDOP
16   = HDOP
17   = VDOP


//-----------------------------------------------------------------------------
$GPGSV

GPS Satellites in view

eg. $GPGSV,3,1,11,03,03,111,00,04,15,270,00,06,01,010,00,13,06,292,00*74
    $GPGSV,3,2,11,14,25,170,00,16,57,208,39,18,67,296,40,19,40,246,00*74
    $GPGSV,3,3,11,22,42,067,42,24,14,311,43,27,05,244,00,,,,*4D


    $GPGSV,1,1,13,02,02,213,,03,-3,000,,11,00,121,,14,13,172,05*67


1    = Total number of messages of this type in this cycle
2    = Message number
3    = Total number of SVs in view
4    = SV PRN number
5    = Elevation in degrees, 90 maximum
6    = Azimuth, degrees from true north, 000 to 359
7    = SNR, 00-99 dB (null when not tracking)
8-11 = Information about second SV, same as field 4-7
12-15= Information about third SV, same as field 4-7
16-19= Information about fourth SV, same as field 4-7

*/

//-----------------------------------------------------------------------------
//
//  Internal code used for NMEA parsing
//
//-----------------------------------------------------------------------------
void ChopIntoLines(std::string &str, std::vector<std::string> &lines)
{
    lines.clear();

	unsigned int strsize = str.size();
    // check if we have any <CR> or <LF> at the beginning of the string
    while( strsize && ((str[0] == 0x0D) || (str[0] == 0x0A)) )
    { 
		str = str.erase(0, 1);
		strsize--;
	}

	unsigned int i = 0;
    while(i < strsize)
    {
        // found <CR>, cut this segment and add into "lines"
        if(str[i] == 0x0D)
        {
            lines.push_back( str.substr(0, i) );

            // check if the next char is <LF>
            if( (i < (strsize-1)) && (str[i+1] == 0x0A))
            {
                str = str.erase(0, i+2);
				strsize -= i+2;
            }
            // no, or this was the end of str
            else
            {
                str = str.erase(0, i+1);
				strsize -= i+1;
            }
            // reset from the start of str
            i = 0;
        }
        else
        {
            i++;
        }
    }

    // save the last bit
    if(strsize) { lines.push_back(str); }
}
//-----------------------------------------------------------------------------
void ChopIntoCommaSepWords(std::string &str, std::vector<std::string> &words)
{
    words.clear();

	unsigned int strsize = str.size();
    // assumed that this is a valid string (i.e. IsLineValid was called), so need to remove 3 chars at the end
    // check that at least we have a "*" as the 3rd char from the end
    if(str[strsize-3] != '*') { return; }
    str = str.erase(strsize-3, 3);
	strsize -= 3;

    unsigned int i = 0;
    while(i < strsize)
    {
        if(str[i] == ',')
        {
            words.push_back( str.substr(0, i) );
            str = str.erase(0, i+1);
			strsize -= i+1;
            i = 0;
        }
        else
            { i++; }
    }

    // save the last bit. If empty, still need to add an empty string
    words.push_back(str);
}
//-----------------------------------------------------------------------------
int HexCharToInt(char c)
{
    if( (c - '0') <= 9)
    {
        return (c - '0');
    }
    else
    {
		c &= ~0x20;
		return (c - 0x37);
      /*  if     ((c == 'a') || (c == 'A')) return 10;
        else if((c == 'b') || (c == 'B')) return 11;
        else if((c == 'c') || (c == 'C')) return 12;
        else if((c == 'd') || (c == 'D')) return 13;
        else if((c == 'e') || (c == 'E')) return 14;
        else if((c == 'f') || (c == 'F')) return 15; */
    }
    return 0;
}

//-----------------------------------------------------------------------------
bool IsLineValid(std::string &str)
{
	unsigned int strsize = str.size();

    if(strsize < 10) { return false; }
    if(str[0] != '$') { return false; }
    if(str[strsize-3] != '*') { return false; }

    int checksum = 0;
    for(int i = 1; i < int(strsize)-3; i++)
    {
        checksum ^= str[i];
    }

    int line_checksum = HexCharToInt( str[strsize-2] )*16 + HexCharToInt( str[strsize-1] );

	return (line_checksum == checksum);
}
//-----------------------------------------------------------------------------
// check that we have digits only, to avoid conversion problems
bool CheckIntWord( std::string &word)
{
    if(word == "")
       { return false; }

	int wordsize = word.size();
    for(int i = 0; i < wordsize; i++)
    {
        if(!isdigit(word[i])) return false;
    }

    return true;
}
//-----------------------------------------------------------------------------
bool CheckFloatWord( std::string &word)
{
    if(word == "")
       { return false; }

	int wordsize = word.size();
    int dot_found = false;
    for(int i = 0; i < wordsize; i++)
    {
        if(word[i] == '.')
        {
            if(dot_found) { return false; } // we have more than one dot
            else { dot_found = true; continue; }
        }
		else if(word[i] == '-') continue;
        else if(!isdigit(word[i])) return false;
    }

    return true;
}
//-----------------------------------------------------------------------------
int ___max_snr = 0; int ___num_sat = 0; 

// var used in this function only, the next expected GPSSV rec number
int ___expected_rec_number = 1; 

void ParseGPGSV( std::vector<std::string> &words)
{
    // check if we have enough words
    if(words.size() < 5) { return; }

    // 3rd word is the record number:
    // check if record number is missing
    if(words[2] == "") { return; }

    int rec_number = atoi( words[2].c_str() );

    // if rec_number = 1, reset vars
    if(rec_number == 1)
    { 
	___max_snr = 0; 
	//___num_sat = 0;
	___num_sat = (___num_sat / 100) *100;		//clear only low part
	___num_sat += atoi( words[3].c_str());		//number of sats in view
    ___expected_rec_number = 1;
    }

    // make sure that we process records in order, i.e. rec match expected number
    if(rec_number != ___expected_rec_number )
    { 
        ___expected_rec_number = 1;
	return; 
    }

    int i = 0;
    while(true)
    {
        int pos = 7 + 4*i; // interested in every 4th word, this is SNR

        i++;  // must increase i BEFORE "continue"

        if(pos >= int(words.size()))
            { break; }

        if(words[pos] == "")
            { continue; }

        int snr = atoi( words[pos].c_str() );

        if(snr > ___max_snr)
            { ___max_snr = snr;  }

	// count non-zero SNR sats
        //if(snr > 0)
        //    { ___num_sat++;  }
    }

    ___expected_rec_number++;
}
//-----------------------------------------------------------------------------
int    ___hour = 0, ___min = 0, ___sec = 0;
double ___latitude = 0.0, ___longitude = 0.0;
double ___hdop = 0.0; double ___altitude = 0.0; double ___geoid_sep = 0.0;

void ParseGPGGA( std::vector<std::string> &words)
{
    ___hour = -1; ___min = 0; ___sec = 0;
    ___latitude = -32768.0;
    ___longitude = -32768.0;
    ___hdop = -32768.0;
    ___altitude = -32768.0;
	___geoid_sep = -32768.0;

    if(words.size() < 13) { return; }

    // word counter
    int pos = 1;

    // word 1 - UTC ------------------------------------------
    if(CheckFloatWord(words[pos]))
    {
        ___hour =  atoi( words[pos].substr(0, 2).c_str() );
        ___min  =  atoi( words[pos].substr(2, 2).c_str() );
        ___sec  =  atoi( words[pos].substr(4, 2).c_str() );
    }
    pos++;

    // word 2 : latitude  ------------------------------------
    if(CheckFloatWord(words[pos]))
    {
        ___latitude =  atoi( words[pos].substr(0, 2).c_str() ) +
                       atof( words[pos].substr(2, 10000).c_str() )/60.;
    }
    pos++;

    // word 3 : N or S for the hemisphere
    if(words[pos] == "S")  { ___latitude *= -1.0; }
    pos++;

    // word 4 : longitude
    if(CheckFloatWord(words[pos]))
    {
        ___longitude =  atoi( words[pos].substr(0, 3).c_str() ) +
                        atof( words[pos].substr(3, 10000).c_str() )/60.;
    }
    pos++;

    // word 5 : E or W
    if(words[pos] == "W")  { ___longitude *= -1.0; }
    pos++;

    // word 6 : fix quality - skip
    pos++;

    // word 7 : num sats in use - skip?
	___num_sat %= 100;		//clear high part
	___num_sat += 100 * atoi( words[pos].c_str() );
    pos++;

    // word 8 : hdop
    if(CheckFloatWord(words[pos]))
    {
        ___hdop =  atof( words[pos].c_str() );
    }
    pos++;

    // word 9 : altitude
    if(CheckFloatWord(words[pos]))
    {
        ___altitude =  atof( words[pos].c_str() );
    }
    pos++;

    // word 10 must be M
    if(words[pos] != "M")  { ___altitude = 0.0; }
    pos++;

    // word 11 : GEOID_SEPARATION
    if(CheckFloatWord(words[pos]))
    {
        ___geoid_sep =  atof( words[pos].c_str() );
    }
    pos++;
    // word 12 : GEOID
    if(words[pos] != "M")  { ___geoid_sep = 0.0; }

    // FIX for some phones which reports altitude in GEOID
    /*if((___altitude == 0.0) && (geoid != 0.0))	// no longer necessary - use WGS84 altitude
    {
        ___altitude = geoid; 
    }*/
}
//-----------------------------------------------------------------------------
double ___speed = -1.0;
double ___heading = -1.0;

void ParseGPRMC( std::vector<std::string> &words)
{
	___hour = -1; ___min = 0; ___sec = 0;
    ___latitude = -32768.0;
    ___longitude = -32768.0;

    ___speed = -1.0;
    ___heading = -1.0;

    if(words.size() < 12) { return; }

    // word counter
    int pos = 1;
    // word 1 - UTC ------------------------------------------        read Time Lat Lon also from GPRMC because some (bluetooth)GPS send GPGGA only 4s but GPRMC every sec.
    if(CheckFloatWord(words[pos]))
    {
        ___hour =  atoi( words[pos].substr(0, 2).c_str() );
        ___min  =  atoi( words[pos].substr(2, 2).c_str() );
        ___sec  =  atoi( words[pos].substr(4, 2).c_str() );
    }

	// word 3 : latitude  ------------------------------------
	pos = 3;
    if(CheckFloatWord(words[pos]))
    {
        ___latitude =  atoi( words[pos].substr(0, 2).c_str() ) +
                       atof( words[pos].substr(2, 10000).c_str() )/60.;
    }
    pos++;

	// word 4 : N or S for the hemisphere
    if(words[pos] == "S")  { ___latitude *= -1.0; }
    pos++;

    // word 5 : longitude
    if(CheckFloatWord(words[pos]))
    {
        ___longitude =  atoi( words[pos].substr(0, 3).c_str() ) +
                        atof( words[pos].substr(3, 10000).c_str() )/60.;
    }
    pos++;

    // word 6 : E or W
    if(words[pos] == "W")  { ___longitude *= -1.0; }
    pos++;


    // word 7 - Speed in knots ------------------------------------------
    pos = 7;
    if(CheckFloatWord(words[pos]))
    {
        ___speed = atof( words[pos].c_str() );
    }


    // word 8 : Heading in degrees  -------------------------------------
    pos = 8;
    if(CheckFloatWord(words[pos]))
    {
        ___heading = atof( words[pos].c_str() );
    }
}

//-----------------------------------------------------------------------------
// Open handle to GPS port, COM0: ... COM14:

HANDLE __hGpsPort = INVALID_HANDLE_VALUE;  // handle to the GPS com port

std::string __save_str = "";               // buffer to store uncomplete data, which has not been parsed yet


bool filemode = false;
extern "C" __declspec(dllexport) int GccOpenGps(int com_port, int rate)
{
	wchar_t comport[10];
	if(com_port == 13)
	{
		wcscpy(comport, L"\\nmea.txt");
		filemode = true;
	}
	else
	{
		swprintf(comport, L"COM%i:", com_port);
		filemode = false;
	}
	__hGpsPort = CreateFile(comport, GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
/*
// for some reason sprintf did not work to write all these in one line ...
if     (com_port == 1)  __hGpsPort = CreateFile(L"COM1:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 2)  __hGpsPort = CreateFile(L"COM2:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 3)  __hGpsPort = CreateFile(L"COM3:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 4)  __hGpsPort = CreateFile(L"COM4:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 5)  __hGpsPort = CreateFile(L"COM5:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 6)  __hGpsPort = CreateFile(L"COM6:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 7)  __hGpsPort = CreateFile(L"COM7:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 8)  __hGpsPort = CreateFile(L"COM8:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 9)  __hGpsPort = CreateFile(L"COM9:", GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 10) __hGpsPort = CreateFile(L"COM10:",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 11) __hGpsPort = CreateFile(L"COM11:",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 12) __hGpsPort = CreateFile(L"COM12:",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 13) __hGpsPort = CreateFile(L"COM13:",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
else if(com_port == 14) __hGpsPort = CreateFile(L"COM14:",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
*/
    if(__hGpsPort == INVALID_HANDLE_VALUE)
    {
        DWORD dwLastError = GetLastError();

        switch(dwLastError)
        {
          case ERROR_ACCESS_DENIED:
            return -1;
            break;
          case ERROR_FILE_NOT_FOUND:
            return -2;
            break;
          default:
            return -3;
            break;
        }
    }

	if(!filemode)
	{
		// COM port setting
		DCB dcb;

		if (GetCommState(__hGpsPort,&dcb))	//fails on some devices with internal GPS - in these cases it's not necessarry anyway
		{
			// Set baud rate and other params
			dcb.BaudRate = (DWORD)rate;
			dcb.Parity   = NOPARITY;
			dcb.StopBits = ONESTOPBIT;
			dcb.ByteSize = 8;

			// use defaults for other fields (found on web)
			dcb.fBinary         = TRUE;	
			dcb.fParity         = FALSE;
			dcb.fOutxCtsFlow    = FALSE;	      
			dcb.fOutxDsrFlow    = FALSE;	      
			dcb.fDtrControl     = DTR_CONTROL_ENABLE; 
			dcb.fDsrSensitivity = FALSE;	      
			dcb.fOutX           = FALSE;	      
			dcb.fInX            = FALSE;	      
			dcb.fNull           = FALSE;	      
			dcb.fRtsControl     = RTS_CONTROL_ENABLE; 
			dcb.fAbortOnError   = FALSE;	      

			if (SetCommState(__hGpsPort,&dcb) == 0)
				{ return -5; }
		}

		// set mask which events to monitor 
		SetCommMask(__hGpsPort, EV_RXCHAR);

		// set buffer sizes (defaults in Windows)
		SetupComm( __hGpsPort, 4096, 2048);

		// Clear all chars from COM
		PurgeComm(__hGpsPort,PURGE_TXABORT|PURGE_RXABORT|PURGE_TXCLEAR|PURGE_RXCLEAR);

		// Setup the comm timeouts. This is important: MAXDWORD for ReadIntervalTimeout means return immediately.
		COMMTIMEOUTS CommTimeOuts;
		CommTimeOuts.ReadIntervalTimeout            = 10;
		CommTimeOuts.ReadTotalTimeoutMultiplier     = 0;
		CommTimeOuts.ReadTotalTimeoutConstant       = 300;
		CommTimeOuts.WriteTotalTimeoutMultiplier    = 0;
		CommTimeOuts.WriteTotalTimeoutConstant      = 10;
		if (SetCommTimeouts( __hGpsPort, &CommTimeOuts ) == 0)
			{ return -6; }

		DWORD dwErrors;
		COMSTAT ComStat;
		ClearCommError(__hGpsPort,&dwErrors, &ComStat);
	}
    // reset read buffer
    __save_str = "";
    __read_lock = false;

#ifdef WRITE_LINES_TO_FILE
    FILE *file = fopen("\\tmp.txt", "w");
    fprintf(file, "");
#endif

    return 1;
}
//-----------------------------------------------------------------------------
// Close handle to GPS port
extern "C" __declspec(dllexport) int GccCloseGps()
{
    int status = CloseHandle(__hGpsPort);
    __hGpsPort = INVALID_HANDLE_VALUE;
    __save_str = "";
    __read_lock = false;
    Sleep(300);   // do not return too fast
    return status;
}
//-----------------------------------------------------------------------------
// returns 1 if the handle to the GPS port is opened
extern "C" __declspec(dllexport) int GccIsGpsOpened()
{
    if(__hGpsPort != INVALID_HANDLE_VALUE) { return 1; }
    return 0;
}
//-----------------------------------------------------------------------------
// Read data from GPS port. Return status flags, as defined below

#define READ_NO_ERRORS 0x01
#define READ_HAS_DATA  0x02
#define READ_HAS_GPGSV 0x04
#define READ_HAS_GPGGA 0x08
#define READ_HAS_GPRMC 0x10

extern "C" __declspec(dllexport) int GccReadGps(int &hour, int &min, int &sec,                  // from GPGGA
                                                double &latitude, double &longitude,
                                                int &num_sat, double &hdop, double &altitude,
                                                double &geoid_sep, int &max_snr,                                   // from GPGSV
                                                double &speed, double &heading)                 // from GPRMC
{
    if(__read_lock) { return 0; }
	
    const int BUF_SIZE = 4096;
    DWORD dwBytesRead;
    char buf[BUF_SIZE+1];

    int return_status = 0;

#ifdef WRITE_LINES_TO_FILE
    FILE *file = fopen("\\tmp.txt", "a");
    fprintf(file, "File handle status: %d\n", (__hGpsPort != INVALID_HANDLE_VALUE ? 1 : 0));
#endif

    // reset output vars
    hour = 0;       min = 0;         sec = 0;
    latitude = 0.0; longitude = 0.0;
    num_sat = 0;    hdop = 0.0;      altitude = 0.0;
    max_snr = 0;    speed = -1.0;    heading = -1.0;

    if(__hGpsPort == INVALID_HANDLE_VALUE)
        { return return_status; }

    // read file
    __read_lock = true;
    BOOL status = ReadFile(__hGpsPort,	 // handle of file to read
                           buf,		 // address of buffer that receives data
						   filemode ? 600 : BUF_SIZE,	 // number of bytes to read
                           &dwBytesRead, // address of number of bytes read
                           NULL		 // address of structure for data
                           );
    __read_lock = false;

#ifdef WRITE_LINES_TO_FILE
    fprintf(file, "GPS status: %d, read status: %d, bytes read: %d \n", GccGpsStatus(), (status ? 1 : 0), dwBytesRead);
#endif

    // clear errors if any
    if(status == false)
    {
        DWORD dwErrors;
        COMSTAT ComStat;
        ClearCommError(__hGpsPort,&dwErrors,&ComStat);
        return return_status;
    }

    // terminate string
    buf[dwBytesRead] = '\0';

    // nothing read - return
    if(dwBytesRead == 0)
    {
        return_status |= READ_NO_ERRORS;
        return return_status;
    }

    // has some data
    return_status |= READ_NO_ERRORS;
    return_status |= READ_HAS_DATA;

	if(filemode)
	{
		char* ptr = strstr(buf+1, "$GPGGA");
		if(ptr != NULL)
		{
			SetFilePointer(__hGpsPort, (ptr - buf) - dwBytesRead, NULL, FILE_CURRENT);
			dwBytesRead = ptr - buf;
			*ptr = '\0';
		}
	}

    // too much bytes indicate lack of computing power
    if(dwBytesRead > 600)
	{
		//MessageBeep(0);	//test
#ifdef WRITE_LINES_TO_FILE
		fprintf(file, "Warning: Too much Bytes read: %d\n", dwBytesRead);
#endif
		if(dwBytesRead == BUF_SIZE)
		{
			PurgeComm(__hGpsPort, PURGE_RXCLEAR);
			MessageBeep(1);	//test
		}
		__save_str = std::string(buf);
		__save_str = __save_str.erase(0, dwBytesRead - 600);	//remove old data (would be overwritten anyway)
	}
	else
	{
		// append to string stored from the last read
		__save_str += std::string(buf);
	}

    // chop into lines
    std::vector<std::string> lines;
    std::vector<std::string> words;

    ChopIntoLines(__save_str, lines);

    // check what to do with the last segment. If it is not complete - store.
    if(lines.size() && !filemode)		//in filemode would store "----- received 7 lines ----" without CRLF!
    {
        std::string &last_line = lines[lines.size()-1];
        if(last_line.size() > 3)
        {
            if(last_line[last_line.size()-3] != '*')
            {
                __save_str = last_line;
                lines.pop_back();
            }
        }
        else
        {
            __save_str = last_line;
            lines.pop_back();
        }
    }

#ifdef WRITE_LINES_TO_FILE
    fprintf(file, "------------ received %d lines ------------ \n", lines.size() );
#endif

    // parse lines
    for(unsigned int i = 0; i < lines.size(); i++)
    {
#ifdef WRITE_LINES_TO_FILE
        fprintf(file, "%s\n", lines[i].c_str());
#endif

        // process valid lines only, i.e. the one with correct checksum
        if(IsLineValid(lines[i]))
        {
            ChopIntoCommaSepWords(lines[i], words);
            if(words.size() == 0) { continue; } 

            if(words[0] == "$GPGSV")
            {
                ParseGPGSV(words);

                return_status |= READ_HAS_GPGSV;

                max_snr = ___max_snr; num_sat  = ___num_sat;
            }
            else if(words[0] == "$GPGGA")
            {
                ParseGPGGA(words);

                return_status |= READ_HAS_GPGGA;

                hour     = ___hour;     min       = ___min;       sec = ___sec;
                latitude = ___latitude; longitude = ___longitude;
                hdop      = ___hdop;    altitude = ___altitude;    geoid_sep = ___geoid_sep;
            }
            else if(words[0] == "$GPRMC")
            {
                ParseGPRMC(words);

                return_status |= READ_HAS_GPRMC;

				hour     = ___hour;     min       = ___min;       sec = ___sec;
				latitude = ___latitude; longitude = ___longitude;
                speed = ___speed; heading = ___heading;
            }
        }
#ifdef WRITE_LINES_TO_FILE
		else
		{
			fprintf(file, "^invalid line\n");
		}
#endif
    }
#ifdef WRITE_LINES_TO_FILE
    fclose(file);
#endif

    /*  debug write (if want to dump the read buffer, if line chopping did not work)
    if(dwBytesRead)
    {
        FILE *file = fopen("\\tmp.txt", "a");
        fwrite(buf, sizeof(char), dwBytesRead, file);
        fclose(file);
    }
    */

    return return_status;
}
//-----------------------------------------------------------------------------
