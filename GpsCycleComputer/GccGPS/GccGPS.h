#pragma once

#ifndef __AFXWIN_H__
    #error "include 'stdafx.h' before including this file for PCH"
#endif

#ifdef POCKETPC2003_UI_MODEL
    #include "resourceppc.h"
#endif

// CGccGPSApp
// See GccGPS.cpp for the implementation of this class
//

class CGccGPSApp : public CWinApp
{
  public:
    CGccGPSApp();

  // Overrides
  public:
    virtual BOOL InitInstance();

    DECLARE_MESSAGE_MAP()
};

extern "C" __declspec(dllexport) int GccGpsStart();
extern "C" __declspec(dllexport) int GccGpsStop();
extern "C" __declspec(dllexport) int GccGpsRefresh();
extern "C" __declspec(dllexport) int GccGpsStatus();
extern "C" __declspec(dllexport) int GccOpenGps(int com_port, int rate);
extern "C" __declspec(dllexport) int GccCloseGps();
extern "C" __declspec(dllexport) int GccIsGpsOpened();
extern "C" __declspec(dllexport) int GccReadGps(int &hour, int &min, int &sec,                  // from GPGGA
                                                double &latitude, double &longitude,
                                                int &num_sat, double &hdop, double &altitude,
                                                double &geoid_sep, int &max_snr,                                   // from GPGSV
                                                double &speed, double &heading);

