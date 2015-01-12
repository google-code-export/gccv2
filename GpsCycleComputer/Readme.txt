
GPS Cycle Computer 4.12

Tip: You can copy this file (Readme.htm) to your Desktop PC and read it there more comfortable.

The latest version and some other tools or information is downloadable at:
https://www.dropbox.com/sh/69ps6iqg4ajau3r/_cXdntYYU6

There is an official forum:
http://forum.xda-developers.com/showthread.php?t=424423 
where you can get any further help or submit any bug reports or feature requests. Here also the latest beta versions are posted.


1. General Description

This is a GPS tracking application for Windows Mobile. You do not need any other GPS software to be installed, i.e. it works directly with Windows GPS driver. You do not need any data network connection to record a log, although there is an option to activate Live Logging (provided by www.crossingways.com), so your position is uploaded to the web site as well.

The tool logs GPS data into binary files with .gcc extension (binary files are much smaller than the text files to store the same information). gcc files then can be loaded back for viewing, or saved into .kml or .gpx format, to view in e.g. GoogleEarth. With the latest version of GoogleMaps, you can view kml files on your phone. Just click on a kml file in File Explorer. Note there is a bug in GoogleMaps installation, unless you install it into the main memory - see section "Useful info" below how to fix this.

To save battery power, GPS can be switched on/off at pre-defined intervals (5 sec ... 1 hours). This has a drawback because the first samples from GPS after switch on are usually not very precise. Therfore there is an option to "Drop first: none .. 32 points".

Main display shows: trip distance; trip time (including or excluding "stop time", i.e. when speed is zero); current / average / max speed; altitude (abs or relative to starting point and altitude gain, that means increasing altitude is summarized); heading; estimated battery usage. Display units are miles / nautical miles / km for distance, m / feet for altitude, mph / kmh / knots / minpmile / minpkm for speed.

Map display shows your track. Also you can use “maps” – any JPEG image with known coordinates of the corners as a background. OpenStreetMap "tiles" are supported - these can be downloaded automatically (if you are connected to the internet), or you can download a set of "tiles" yourself and copy them on your phone. You can also load a "track-to-follow" from gcc, kml or gpx file, which is displayed in different color.

2. System requirements

The tool runs on mobile phones or PDAs with operating system Windows Mobile 5.0 and above. On Windows for Pocket PC 2003 you need to install ".Net Compact Framework 2.0".
GCC needs a touch screen and a GPS device, either internal or external (also bluetooth) as long as it is accessible via COM port.
Screen resolutions are tested from 240x240 to 480x800.

3. Installation

Copy the GpsCycleComputer.CAB file into your Windows Mobile device and click on it from your phone FileExplorer to install. The tool can be installed into any folder, so probably it is better to install it on "Storage Card" or into "Internal Storage", to save main memory. The path to the tool (on UK Windows) will be then \Internal Storage\Program Files\GpsCycleComputer. You can also find this Readme.htm file in that folder, if you want to read it on your phone.

The tool shall work on any screen resolution in portrait or in landscape mode. It was designed on a VGA screen, so you might have small distortions on other screens due to scaling. For landscape mode, the buttons are moved to the right to save space in order to keep the layout basically the same.

The tool now supports AppToDate, i.e. if you have AppToDate installed on your phone (get it from here http://www.apptodate.org/get , more info available here http://forum.xda-developers.com/showthread.php?t=327990 ) - then you will be receiving latest GpsCycleComputer version automatically.

All CAB versions and the complete source code (in C#) are currently stored at Google Code project page: http://code.google.com/p/gccv2/downloads/list

4. Quick start. Controls on the "Main" screen

As you start the tool, you see the main screen with usual "cycle computer" info: trip time, clock, speed (current, average and max), and the info you would see on a GPS device: altitude, heading, latitude/longitude, plus some info about the GPS status. At the bottom there are 3 buttons: Menu (left), Start (center) and GPS (right). These 3 buttons changes as you move to different screens or start/stop GPS.

Important: you can always come to the Menu page by long-click (hold more than 0.5s) or double-click on the left button (no matter what it shows).

In most cases you shall be able just to click Start button, get GPS lock (so it is better to try this outside! ) and record you first track. Find an open place outside (which is not surrounded by trees or buildings), click the Start button and stay still (this helps to get GPS lock). You need to watch:

GPS display (bottom right of the main window) :

    S - number after "S" is number of satellites in use and satellites in view (after slash).
    Snr - number after Snr is max Signal-To-Noise ratio, i.e. the signal quality. The higher, the better. Number below 20 is very low, and it is unlikely you will get a lock in this conditions.
    T - number after T is difference in seconds between current UTC time of your phone and UTC time of GPS sample (from satellite). Might be negative - then you phone time is behind. What is important, is when the number is increasing, then the GPS cannot get hold on a fresh sample (i.e. searching)
    Dh - number after Dh is "DHOP" == Horizontal Dilution Of Precision. 1 is very good lat/long precision, 50 is very bad lat/long precision.
    Rectangle color: Green - GPS has lock, sample OK; Red - sample is bad/old/invalid, etc, GPS still searching; Yellow - GPS valid, but first samples dropped to get better precision (useful in start/stop mode); Grey - GPS suspended to save battery (start/stop mode).
    Compass - shows the direction of movement (north above). By means of context menu it can be changed, so that the arrow points to north, provided that you move in direction from bottom to top of your device.

Info display (bottom left of the main window) :

    “gps on” – ?GPS: Off, On, Logging or Paused ? self explanatory. 
    “gps sec” – ?GPS: suspended for xs" ? gps is switched off for given time interval to save battery, as selected in Options.
    "GPS: stopped on low power" ? gps is stopped due to low battery (<20%), as selected in Options (default).
    “gps Number” – ?dx? ? if x > 0 this means data is dropped to get better precision, as selected in Options.
    "TLLAHS" ? T: time; L: latitude; L: longitude; A: altitude; H: heading; S=speed. Capital letters means record OK, small letters means not OK.
    Number at the end is the time in seconds since the start of the search for valid signal, reset after – – position record found, T – time record found, S – 180s (need T and LL to get lock. Also a check is made that time is increasing, so data is fresh).

If you have number of satellites 0, or "no data from GPS" for some time, then it is likely that your GPS setting are not correct.You need to read further how to set GPS options correctly, see section "Useful info", "GPS setup" below.

Assuming that all works (you see "GPS: on" and green rectangle) with the default settings (or if you have changed the setting, and it works!) , let us proceed with the desciption of the main screen.

When running, Start button changes to Pause - to pause GPS. Press Pause button again to continue logging. The GPS button changes to Stop - to stop the log. The Menu button changes to Maps - which shows your track. Remember you can always get to the Menu by long-click (or double-click) on left button. On the Maps screen, you can use Zoom in / Zoom out buttons, and move track line with your finger (like in GoogleMaps for Mobile). Also you can plot a "map" as a background for the track - see section "Maps" how to do that.

When GPS is on, the tool will prevent your phone going into standby mode (note, the standby time has to be larger than 15 sec), so the screen shall be always on. By default, you can use the hardware "power" button to switch off the screen – GPS will continue logging. On some devices this switches off also the GPS receiver, so track logging is no more possible. Then you can try to check the option "GPS | use alternate method to keep GPS on". This modifies some values in the registry while gps is on. If this doesn't solve the problem you can enable the option "Main screen | Show BKLight Off button" and use this button to switch off backlight instead of the power button.

When logging, if your current speed is not 0, you shall see "heading" (blue arrow at the bottom right of the main screen or in center of map screen). This shows direction of your travel, as it looks on a map with "north", as usual on a map, pointing up. Do not be confised - the arrow shows direction of your travel, not the "north" direction (north is up).
Hint: you can configure the arrow in main screen to behave like a compass that means it points to north.

Now, if you have any data logged, you can stop the logging (press Stop button), and see what you can do with the log file.

5. Menu page

By clicking the Menu button or always by long-clicking or double-clicking the left button you get to the Menu page. This is the central navigation point through all the functions of the application. The three buttons at the bottom are fixed and allow to get back to Main screen or to scroll down and up the menu items (you can also drag on the menu to shift up and down). Currently not available functions are indicated by greyed buttons.

Functions:

    BKLight: by default, you shall be able to switch your phone off by the hardware power button, and the GPS will continue logging. If this does not work on your phone (or you do NOT want to use "read GPS data directly" settings) - then you shall use this button to switch off the screen during logging. Remark: the buttons keep enabled so you can reactivate your device. With a click on the left half screen you return to the main, on the right hafl screen you return to the map screen. 
    Map
    Options: (see below)
    Altitude: show altitude graph over time (see Graph below) 
    Speed: show speed graph over time (see Graph below) 
    Add Waypoint: add a named waypoint to the track log.
    Additonally you can record a short Audio note which is saved in the tracks directory with the track name extended by a number and with .wav extension. Recording starts automatically by pressing the "Audio" button. "OK" stops recording and dismisses the dialog. The waypoint in the track log includes a link to the audio file. 
    File/Export: file operations like load GCC files and save KML or GPX files 
    Track2Follow: load a track as second line in map which you can follow. Valid file formats are GCC, KML and GPX. With context menu on this button you can display the track statistics of the T2F. 
    Restore: restores the last session (loaded Track and Track2Follow). This is done automatically after an abnormal termination (crash). 
    Exit
    Navigate: The navigation screen shows a big arrow which points in the direction which you should drive to follow the Track2Follow. The arrow points at a point 100m ahead on the T2F, based on your current direction of movement. At the bottom the distance to the destination is displayed or the shortest distance to the T2F if it is more than 100m. By means of context menu you can navigate backward on the Track2Follow. Also you can configure if you want to see the navigation button in the bottom menu column (note: go to menu page - Navigate to display navigation screen to reenable this feature; note2: from the map screen you can get to the navigation screen by a click on the navigation arrow in the upper right corner of the map).
    You can enable or disable voice commands which guides you audible along the T2F. You can select the language in the first option page. To create your own voice see Readme.txt in subdirectory 'local'. A click on navigation screen repeats the last active voice command. 
    Lap: (see below) 
    Recal 1 .. 3: with these 3 buttons you can recall different option settings. Use the context menu to choose the name and save the settings. Please note that the names of the buttons are part of the setting itself, so some of the 3 buttons may change their name when you recall a setting. If desired rechange the name and resave the setting you have just recalled. The settings are stored in the application directory in files with the name of the setting extended with .dat. The name of the current loaded setting is displayed in Main display at the top of the Info field
    ClearTrack / Clear2Follow: clears the main track or 'track to follow'. 
    Input LatLon: is useful to browse map on a specific place or to navigate to a specific position (e. g. Geocaching).
    The input is allowed in any format (dd.dddddd or Ndd°mm.mmmm' or Ndd°mm'ss.ss"). The characters (N,S,E,W and ° ' ") can be omitted or replaced by sign or blank respectively. Lat and Long are separated by semicolon or by the first Letter of Longitude (E, W).
    Continue: continues a previous recorded track. A warning is displayed if you try to continue a track which is more than 1km distant.
    Heart Rate: show heart rate graph over time (see Graph below)
    Help: displays "Readme.html" in your browser.
    About: displays "About page".

 
6. Working with log files

A log file is automatically created as you click Start button. The default name "year"+"month"+"day"+”_ “+”hour"+"min"+"sec"+ .gcc. The files are stored in the directory which you can set in Options. By default this is  a subdirectory "tracks" of the directory where the executable is installed. It is also possible to enter a custom file name (see Options). Also a file ?log1.csv?, located in the same folder as gcc files, is updated with the summary of the trace (one line per trace / gcc file) – to give you a quick overview of all your logs in that folder.

To go into Files screen, click Files button (Options | Load GCC / Save KML- GPX-file ...). You shall see a list of *.gcc files stored in the directory you selected (note, you cannot change the directory in the Files screen, you need go to Options to do that.

The buttons on Files screen are: on the right there are buttons to select next/prev file. Also you can selected a file by clicking on it. The center button on the bottom row - loads gcc file (and automatically goes to the Maps screen to view it). The left button on the bottom row - close the Files screen and return to Main.

Left and center buttons on the top row save the selected gcc file into KML or GPX - these are two popular formats to exchange GPS data. GCC is the internal format for this tool, so you must save file as KML or GPX before viewing them in other applications. When you save file into KML, a "*" is printed before the file name. If you save into GPX, then a "+" is printed - so you can quickly check which files have been converted into GPX and/or KML.

KML files can be viewed with GoogleEarth. Also, with the latest version of GoogleMaps, you can view KML files on your phone. Just click on a kml file in File Explorer (note that bug in GoogleMaps install - see Useful info, Problems with GoogleMaps installation.

To view GPX files you can try http://utrack.crempa.net/ web site. Also you can view GPX files in GoogleEarth (go to File | Load menu).

GCC files are assosiated with GPS Cycle Computer, so you can also open gcc file by clicking on it from FileExplorer on your phone. But the tool does not load the settings correctly in this case, and also if you have the tool already opened, it does not load another file again (i.e. you need to close it, to get it load a file). If you know how to fix this - please let me know. So at the moment this seems not a very usable option to open gcc files (i.e. better to load them from the tool, as explained above)

7. Options

Options can now be reached from any state of the application by means of the Menu page. This way you can change e.g the logging interval, load different maps or track-to-follow. Note that even if you change the input/output file folder, the currently logged file still be written in the folder you set as you started the log, i.e. it will not be changed. If you change GPS port settings while GPS is on, you must switch off GPS and switch on again to have the changes take effect.

There are a few option pages, so press "next" or "previous" (center or right buttons) on options tab to switch between option pages.

The first options you see:

    Show/hide option view selector ... - there are quite a few options pages, so it is likely that you might not want to scroll over all of them. This option lets you choose which option pages you want to see. By default, all pages are shown.
    Set maps files location ... - specify the location for jpeg or OSM maps files. You can select existing folders only, so please create new folders with File Explorer. By default, this is set to the sub-folder "maps" of the location of the GpsCycle executable.
    Set input/output files location ... - specify the location for input/output files. By default, this is set to the location of the GpsCycle executable. You can select existing folders only, so please create new folders using File Explorer. Also you can use File Explorer to rename/move/copy gcc files, if required.
    Set local\language directory ... - select the language/voice for the spoken voice navigation commands.
    Status display of Current File Name, Track to Follow and Info. 

Page "GPS options":

    GPS activity : choose how often you would like to run GPS (always on, or switch it on/off at given intervals)
    Drop first:: ignores the first "x" points after GPS gets a lock. The first points might have a low precision, so you might want to skip them.
    Stop GPS if battery < 20% : is a safety feature, to avoid completely draining the battery. Default on.
    Read GPS data directly – please read "Useful info", "GPS setup" section. If “off”, the Windows “parsed” GPS driver is used, which does not require any options to be entered by the user - but this does not always work well, also you cannot use hardware power button to switch off the phone, leaving GPS running. So the default/recommended is "on”, COM4, 4800 baud rate (works fine on most phones with WinMobile 6.1). In this case the tool reads and parses the GPS data directly, and the "raw" Windows driver is used. The functions from “own” GccGPS.dll are called, which, I think, might work a bit better than the Windows “parsed” driver. For test purposes you can select "\nmea.txt" instead of COMx. Then the raw NMEA data are read from that file (in root directory) instead from the GPS receiver.
    WGS84 altitude - display altitude with respect to WGS84 ellipsoid instead of Mean Sea Level. Some phones report altitude in wrong fields, so test to check this, if your altitude reading is not correct. Default off. 
    corr, m - if your GPS hardware does not correct the altitude to the right Mean Sea Level (e.g. on Diamond), then you can manually enter the value to display correct height above the sea level. The correcton values (called GEOID) for your location can be computed here: http://earth-info.nga.mil/GandG/wgs84/gravitymod/wgs84_180/intptW.html?
    Beep on GPS fix - if checked, beeps on GPS fix and loose.
    AVG - averaging of the GPS data. This feature is experimental. Set to 1 disables averaging. I don't recommend to set the average factor higher than 3 (at current implementation).
    use alternate method to keep GPS on - check this option if GPS goes off when you switch off the screen (see also Main screen options, Show BKLightOff button). In this case some registry values are modified to keep GPS on.
    Safe Energy - GPS off on power off - check this option, if you want to extend the battery lifetime and not log your track. If the option is activated, and the device is switched off, the GPS receiver and the application is powered down. This option is not supported by all hardware devices. Successfully tested on HTC TD (WM6.1) and HTC TD2 (WM6.5), but not supported by e. g. Samsung Omnia 2 (WM6.5) and Asus ASUS P320 (WM6.1)
    Safe Energy - do not keep Backlight on - check this option, if you want to extend the battery lifetime and you do not need to keep the display always on. If option is activated, the Backlight is switched off, if you do not use the touchscreeen for a predefined time (Windows power management settings - battery power).

Page "Main screen options":

    Units : select units for the display and graphs.
    Exclude stop time : if activated, the points with zero speed are removed from "trip time" and "average speed" calculations. This is useful to see the "net time" when you e.g. cycling and make breaks during the trip, without the need to switch off the logger. By context menu you can choose another option (...+ beep) which beeps whenever GCC recognizes a start or stop.
    Ask for log file name – show an edit box, so you can enter a custom log file name before starting logging. Default off (the file name generated automatically).
    Single Tap for Config - when "on", you can configure many fields in Main screen with single tap instead of double tap. Default off.
    Confirm 'Pause' and 'Stop' - shows a confirm message to prevent accidently interrupting the track.
    Keep touch on while backlight off - this allows to reactivate the display with a touch on the screen. A click on the left half screen brings you to the main display, a click on the right gets you to the map display.
    Show 'Exit' after Stop - this shows temporarily the Exit button on the right, so you can quicker shut down the application.
    Change menu 'Options' <-> 'BKLight' - this changes the position of the two buttons in the menu page, to have easier access to the buttons you need.
    Select fore/back/mapLabel-color... : you can select the colors from a color stripe or enter RGB values.
     Data array size: - here you can select the size of the internal data arrays. Default is 4k (4096). If logging once a second this allows data storage for 1h 8min without data decimation (when decimating, every second point is deleded). If you change the array size the data gets lost. If you use big arrays GCC may slow down significantly. The data arrays uses 40 bytes per point. 

Page "Maps screen options":

    Plot track as dots - plot each track point as dot, or connect them with lines.
    Track – let you change the color and width of the track. The same line width and color settings are also used for the line saved into KML file.
    Plot track to follow as dots - for "track-to-follow" line: plot each track-to-follow point as dot, or connect them with lines.
    Track2f - for "track-to-follow" line: let you change the color and width.
    Display waypoints - if this option is checked, and the track 2 follow contains waypoints, the position and name of the waypoint is displayed on the map screen.
    White background - By default, the tool background color is used on the map page to plot background. If you want to make the maps background always white (regardless of tool background) - set this on.
    Multi-maps - option to plot multiple jpeg images at the same time. This takes more memory and is slower, but gives a better picture. Default is off (i.e. a single, the best, map is plotted) - this is good if you have just a few large map images. For OSM maps (as these a small, 256x256 images) it is better to plot multiple maps. With this option, you can select how many maps to plot at the same time (up to 8), and also which map scale considered as "the best" - this is used to decide in which order to overlay maps on the top of each other (you can have 1x zoom, 2x zoom or 4x zoom). Try to experiment with different setting, to see which one works best for you.
    Download - automatic OpenStreetMap download (make sure you have internet connection on your phone, and remember about the network charges!). Default is OFF (i.e. only the maps which are already stored on your phone will be used). Otherwise you can choose which maps server to use. Note that different OSM servers have maps in different "styles" (e.g. "cycle map" or "osmarender"), so you need to set different folders for different maps. A good idea is to create a new empty folder for each map style (i.e. each server) using FileExplorer. To specify the map folder, click "Set maps files location" button on the first page of "Options". The server address and Download ON/OFF is stored for each map separately. For more information see section 10.5 "Automatic OpenStreetMap download" below.
    Default zoom - specifies the default distance in meters which is displayed in either direction around the current position.

Page "KML/GPX options":

    Save altitude to KML - save altitude data to KML file, and set <altitudeMode> tag to "absolute". With this option on, you shall see a 3D line above terrain. When this options is off, then the track line just follows the terrain. Note the absolute value for the altitude is offset by GEOID value (see option below), so you can move the line up/down, if required, by changing the GEOID setting before saving the KML file. But often, due to unaccurate altitude reporting by GPS, the track line goes underground and dissapears on GoogleEarth display. So if you are on the ground (not e.g. flying, etc), then it is better to keep this option off, and just let the line to follow terrain. Default off.
    Save GPX with rte tag - use <rte> tag instead of "<trk>" tag - try to save GPX files with this option on or off if you have problems reading GPX file with another application. Default off. This option should always be off, unless you can solve the mentioned problems. 
    Save GPX speed in m/s - save speed in m/s instead of km/h (which seems a default for GPX). Default off.
    GPX time adjustment, hours - by default, the time saved into GPX file is the "local time" when the track was recorded (this is what is saved into .gcc file). But some tools (and the GPX spec, actually), needs the "UTC time". So you can use this option to do the corresponding time adjustment. "+1" means that 1 hour will be added to every time sample before saving into GPX file.
    Separate GPX trkseg if gap > 10s - if checked and the logged track is interrupted for more than 10s (e.g. by pressing Pause or failing GPS signal) the track in GPX export will be separated in different track segments. 

Page "Live logging options":

    "Live logging options" page, provided by www.crossingways.com.You need to create an account at that web site, to be able to use this feature. Please visit web site for all info ( I am afraid I cannot answer your questions about this feature, but I will try to arrange for someone from crossingways to help)
    "Hide/show keyboard ..." - click to show keyboard, if you need it to type username and password.
    "Server URL" - default is "http://www.crossingways.com". This Textbox allows you to specify an alternate server which must be compatible to crossingways. 
    "User name" and "Password" - as used to log into crossingways web site. You need to enter this only once, then it is stored on your phone (in encrypted form). After you typed these information, make sure you have a data connection on you phone (or connected with ActiveSync or WiFi), and click "Verify login ..." - to check that you can login OK to the web site. If you have a message that it has been verified, than your name/password is stored, and you can proceed with live logging. If you have an error message, then you need to check your data connection, and/or check if you have your name/password correct (e.g. make sure that you can log into crossingways web site).
    "Live logging": select "off" to disable this feature, or set a time interval to upload your position to the web site.

    When live logging is activated, you will be asked each time you click "Start" button, if you want to proceed. Also in the "info" box you will see messages "livelog ok" or "livelog error!"  - which tells you the status and time of last log attempt..

and finally:

    "About" page, where you can check the current tool version and see other important info including a button to display this readme file..

Some fields in Main page or map screen can be configured by context menu (press and hold for about 2 seconds to open the context menu).

Context menu "Main page":

    "Time": By context menu on the Time field you can choose between display of "inclusive stop" and "exclusive stop" of the trip time. You can switch on "beep on start/stop" and you can configure "not to log passive time", that is the time when logging is paused or off. In this mode you loose the absolute time respect of your log. 
    "Clock": By context menu of the clock you can maximize the App (overlapping the title bar) and activate day or night scheme.
    In addition you can choose to "sync with GPS", then the system time and date is synchronized with the GPS time when the GPS is switched on and has its first fix. This function is disabled if there is an active log, to prevent time anomalies. So if you wish to sync the time before continuing a log, switch on GPS and wait for fix (update of clock) and then continue the log. 
    "Speed": Directly in the main screen by context menu of the Speed field you can choose if speed is taken from the corresponding GPS sentence or if it is calculated from the moving position (or both).
    In addition you can enable heart rate support here (see section 13. Heart Rate Support).
    "Altitude": In the upper region you can choose between "absolute" and "relative" Altitude (relative to the start of trip).
    In the lower region of  Altitude field you can choose between "Altitude gain" (sum of increasing altitude), "Altitude loss" (sum of falling altitude), "Altitude max" (highest altitude), "Altitude min" (lowest altitude),and "Slope" in percent. You can also choose "<sequential>" that means all 5 values are displayed automatically in sequence, each for 4 seconds. If you tap into the field the sequence stops or restarts again.  
    "Distance": In the context menu of the Distance field, you can choose between the distance since start of the trip, the distance to destination or between the current position and the track to follow start or track to follow end position (bee-line, not real remaining length of the track). Further you can change the display to "ODO" (overall distance). You can set your initial ODO in "gccState.txt" (first line in m; hint: 1 mile is 1609.344m). Don't change anything else!.
    "GPS": By context menu you can choose to have a beep on GPS fix and you can configure to have the navigation info here.
    The compass can be configured to show "north" instead of "heading" (direction of movement). You can select to log the raw nmea data in \tmp.txt. The compass style can be set to "graphic", "digital" or "letter" (with needle). Finally the format of Lat and Lon can be altered.

Many options can be altered by double tap in the corresponding field (if you check "Single Tap for Config" in Options - Main Screen, then single tap is enough).

Context menu "Map screen":

On the Map screen you have the following options in the context menu:

    "Reset map (GPS/last)": The current or last position is displayed in the center of the screen. If you move, the screen will follow your movement. This option has the same effect as an double click on the map screen.
    "Undo reset map": restores the last view before reset map. 
    "Show track to follow - start / end:": By selecting this option, the start / end position of the track to follow is displayed in the center of the map screen.
    If one of this options is activated, the map screen will not follow the current position, even if you move. This options are only available, if a track to follow is loaded.
    "Edit t2f (add points)": You can add points to the "track to follow" direct in map click by click. To leave the edit mode select this menu point again. 
    "Add waypoint": You can directly add a waypoint to your current tracklog. This option is only available, if a tracklog is recorded.
    "Show waypoints": If a track to follow is loaded and includes waypoints, you can select to show or hide displaying waypoints.
    "Navigate backward": navigate the opposite direction than the t2f was recorded.
    "recalc min dist. to t2f": in very rare conditions GCC navigation could got stuck at a point in t2f which is not the minimum distance to your current position, then use this option to recalculate the min distance.
    "Reload map tiles": reload all visible map tiles from osm server.
    "draw map while shifting": the map is redrawn full while shifting (needs more powerful device), otherwise black areas are visible while shifting.
    "config map display": here are many sub menues to customize the map display. On the bottom of the display you can configure up to 3 fields to show numerous infos like time, speed, distance, battery or others. In t2f mode you can have a different configuration.


8. Graph

Display graph of altitude or speed (or heart rate if enabled) over time or distance. You can zoom the x-axis with the zoom buttons or you can zoom either axis separately by dragging a point at the axis (outline) of the drawing in either direction. In other words you can 'take' for example the point "2 min" and dragg it to the desired new position, either right or left. Dragging inside the drawing moves the graph. A double click does autoscale to fit the whole data. If you have zoomed in and logging is active, the first "autoscale" changes to "track mode", this means zoom is preseved but the current value is kept visible. The second "autoscale" does fit the whole graph.
Double click on the top label changes between Altitude and Speed (and Heart Rate if enabled).
Double click on the bottom label changes between Time and Distance.

If you have loaded a Track2Follow, the altitude graph of this track is shown in the background in different color. The nearest point of this track to your current position is denoted by a Point. If you enable in context menu "align t2f" then the Track2Follow is shifted in x direction to match the current position.

9. Lap

A lap can be defined in different ways. Either "manual" by clicking on the Main screen (not on the buttons) or distance based (400m, 1km, 2km, 5km) or time based (1min, 2min, 5min). The statistics of all your laps are displayed on the lap screen. You can export the data in csv format by clicking "export .csv". The directory and file name is the same as the track but with extension .csv. In addition after every lap a waypoint with the lap data is created in the track (except in manual mode because this is used usually on a round trip and the waypoints would fall all on the same place).

If the lap function is enabled the "Altitude" field in the main screen changes to "Lap" field. It displays the time (in distance based mode) of the current (and the last lap). If current lap has just begun (<5%) the current lap value is just the last value (extrapolation would be too inaccurate). For the rest of the lap (5% to 100%) the current lap value is extrapolated to the whole lap distance so that the current value can easily be compared to the last value.

10. Maps

The most powerful and most convenient method of using maps is to use OSM-type maps, described in chapter 10.4.

A “map” is any JPEG image with known coordinates of the corners. It is plotted as a background on “Maps” page. Maps are automatically selected – the map which has better coverage of the picture (in % of the picture area) and higher resolution (lower km per pixel) is selected. Also there is a option to plot multiple maps at the same time - read below how it is implemented.

So first you need a jpeg image, the file must have .jpg or .jpeg extension, e.g. “MyTown.jpg”. Do not create very large images, as it takes long time to load and plot them - a good size is about 1000x1000 max.

To specify the map coordinates, you can use 3 methods described below. Also you can use "OpenStreetMap tiles" - these are specially created PNG images, so the image coordinates can be derived from the file names. I.e. you do not need to specify the coordinates for OpenStreetMap tiles - just download them.

10.1 txt + jpeg: first method is to create a text file (with .txt extension and the same name as the image, e.g. “MyTown.txt”) which contains 4 lines: 1st line - latitude of the bottom left corner, 2nd line – longitude of the bottom left corner, 3rd line - latitude of top right corner, 4th line – longitude of the top right corner. The lat/long must be in decimal format, e.g. 55.976598, just the decimal number, no degrees character, etc.

You need to place the jpg and txt files into folder with your maps. By default, this is the “maps” subfolder where GpsCycleComputer executable is installed on your phone, e.g. if you install into “Internal Storage”, then the default location (on English Windows) will be: “\Internal Storage\Program Files\GpsCycleComputer\maps”. This folder is created during CAB installation and has a text file “maps_folder.txt” inside, so you shall be able to identify it easily. Note that you can change the maps location in Options. The tool supports up to 512 “maps” per folder.

10.2 kml + jpeg: second method is to use GoogleEarth to create maps, it is quick and accurate. A very good and detailed description how to create such a map is given in the RunGps manual, section 5.2.2 (look at http://www.rungps.net/wiki/DownloadsEN). The only difference is that you need to save location as “.kml” file, not as “.kmz” file used by RunGps. If you already have a .kmz file, then it is easy to convert it to .kml: simple change the .kmz extension into .zip (this is a zip file), and unzip it – the unzipped file will be the .kml.

So here are the main steps:

    Start GoogleEarth and select the area to be used as a map.
    Make sure that the “Terrain” layer is OFF, otherwise the map will be distorted (the layers are shown in the side bar)
    Make sure the map is not rotated, i.e. the north is up. Press “r” (in English version) to “reset” any rotations.
    You can also switch on grid (Ctrl-L), to check that the map is not rotated – but better to save picture without grid.
    Save the image (Ctrl-Alt-S) as a jpeg file.
    Now add a placemark (Ctrl-Shift-P), do not move/edit it, just press OK to put it in the centre of the image.
    Save this location (Ctrl-S), select “.kml” file, and give it the same name as the .jpg file for the map.

This is it! As with the txt files, you need to place the jpg and kml files into folder with your maps (by default, this is the “maps” subfolder where GpsCycleComputer executable is installed).

Here is my interpretation how this works for kml files: The .kml file is just a text file. It contains the coordinate of the map centre (LookAt point) and the height of the “eye” This is a single point, but as I understand, the default angle of view along the horizontal (x-axis) is 60 degrees, so knowing the height of the “eye” and this view angle allows to determine the map size along x-axis. As we know the picture size in pixels, then we can determine the y-size of the map.

10.3 gmi + jpeg: third method is to use GpsTuner' gmi files. Instead of the coordinates of the map' corners, you can supply coordinates of a few reference points (e.g. a bridge or road intersection). gmi file is a text file which contains the following lines:

Map Calibration data file v3.0
Image1.jpg
1025
1859
879;1153;8.96395330;45.50307600
685;332;8.94953370;45.54786704

First line is the header (must be present, otherwise an error dialog is shown). Second line is the image file name. It is not used by GpsCycleComputer, as the gmi and jpg file must have the same name. Lines 3-4 are the image width/height - these must match the image size, otherwise an error dialog is shown. This is an important check, as if the image size does not match these values, then the lines below will be incorrect, as they give location of the reference point on the image.

The format of these lines with reference points is: 4 values separated by ";" - the x and y point location on the image (in pixels), then longitude and latitude of the point. You could have a few reference points/lines, but not less than 2. Note that the "y" coordinate in pixels is calculated from the image top, not from the bottom - e.g. if you load your image into Windows Paint, and move mouse over it watching the mouse position display, you will see that the image origin is the top left corner, not the bottom left.

Note that the map must not be rotated, i.e. the north must be up (as with all other maps used by GpsCycleComputer).

There is a freeware Map Calibrator, which simplifies greatly the generation of the gmi files. As image size is limited on Windows Mobile, it also can split a big jpg into several tiles (uncheck the option: 'use only one calibration file..').

As with the txt and kml files, you need to place the jpg and gmi files into folder with your maps (by default, this is the “maps” subfolder where GpsCycleComputer executable is installed).

A similar idea to set the map coordinates is used in OziExplorer map files and CompeGps imp files. To convert these files into gmi files you can use this web site: http://www.map-imp-gmi-converter.com/index.php

10.4 Using OpenStreetMap tiles - these are PNG images, arranged into a special directory structure based on "zoom level" (0 to 18), and the tile X coordinate within this zoom level. The image Y coordinate is the file name. More info is here: http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames. All images are 256x256 in size.

As an example, assume that you have a directory "tiles", then first you will see sub-folders with names "0" to "18" (this is the zoom level, not all directories will be there, i.e. you will see the level which you have downloaded). Then inside each sub-folder you will see more sub-folders, each has name as a number - these are the X-coordinates of the tiles. And fnally, you will see the .PNG files itself - the file name is the Y-coordinate.

To use tile files in GpsCycleComputer, you need to select the top folder, i.e. the folder "tiles" in the above example, the one which have sub-directories 0 to 18. Note that you should not change the directory structure (then you will get incorrect tile coordinates), or to place any other files there (they will not be loaded, i.e. only the PNG files will be used).

Since version 4.3 GCC supports other image formats in OSM style maps. Besides .png also .jpg .bmp .gif .exif .tiff are supported. But all images of the whole map must be of the same type. You can have separate maps with different formats in different directories.

There is no limit on the number of tiles (just the size of you phone's storage card!). The large number of tiles will not slow the tool or increase the memory usage - as only the tiles which are required are loaded for plotting.

There are a few tools to download tiles, I tried "JTileDownloader" which is available from here: http://wiki.openstreetmap.org/wiki/JTileDownloader. As the tiles are downloaded, they are automatically arranged in the directory tree described above - so all you need is to copy it to your mobile, and point GpsCycleComputer to the top folder (e.g. "tiles").

My favorite is another powerful tool "Mobile Atlas Creator" to download tiles, which is available from: http://mobac.sourceforge.net/. Mobile Atlas Creator supports many map sources and can create different map formats. The format "OSMTracker tile storage" is compatible to GCC. In some cases it may be advantageous to apply the "custom tile processing" for example to convert 170kB .png files into 25kB .jpg files (the x/y size must be kept at 256x256!).

If you want to download maps with your mobile device, there is a small tool "PDATileManager", with which you can download tiles for a whole area.

Please don't use these tools for bulk downloading of large areas, because this creates a high load on the tile servers and the application will be banned.

For more info visit www.openstreetmap.org.

10.5 Automatic OpenStreetMap download - if you have an internet connection on your phone, then you can select an option to automatically download OSM tiles which do not exist on your phone, and are required for the current display. To enable this option, select a server name to be used from "Download" combo-box and check the Download checkbox. Note that different server have maps in different "styles", so you need to select different folders for different maps. If you are using this option for a first time, just create a new blank folder with FileExplorer, and use "Set maps files location" button to select this folder.

If you already downloaded tiles from a server (using e.g. JTileDownloader), then select that server name and select the folder you already created to download the tiles, so the tiles which are missing and requried for display will be downloaded there. This way your "tile collection" will grow. Note that if a tile file already exist, then it will not be downloaded automatically. If you want to download the visible tiles again, select "reload map tiles" from context menu.

There is a 30 second timeout for a tile download, so if a server does not reply within this time, you will see "Download Error" on the map display. This could happen if a server is busy, so you can try again - just move the map with your finger or press zoom buttons - the map update will trigger download.

The download servers are defined in the file "osm_servers.txt" in the application folder. (the former file osm_server.txt is no more used.)

If the file osm_servers.txt doesn't exist it will be created with the predefined list of servers:

    Osmarender http://a.tile.openstreetmap.org/
    Cyclemap http://c.tile.thunderforest.com/cycle/

You can specify any additional OSM server, if requred. The osm_servers.txt shall contain in each line the name of the server (which is displayed in the combobox) and separated with a semicolon (;) the http server address. The server address shall finish with "/", as the required zoom, X coordinates and Y.PNG file name will be appended after the "/", e.g an example address could be http://a.tile.openstreetmap.org/10/12/33.PNG. The corresponding line in osm_servers.txt should be:
OpenStreetMap; http://a.tile.openstreetmap.org/
It is a good idea to try to access such a sample address using a web browser, to check that you can see PNG images at that server, i.e.the server is working. A list of tile servers is available at: http://wiki.openstreetmap.org/wiki/Tileserver.

For each map (directory) there is the corresponding download enable state and download server stored in a file "server.txt" in the corresponding map directory.

The copyright info in the file copyright.txt in the map directory is displayed in the lower right corner of the map to fulfill the requrements of some map sources.

For usage of some OSM servers the following conditions apply:
Maps © Thunderforest, Data © OpenStreetMap contributors. 

10.6 Map selection. The maps are automatically selected - as you touch and lift you finger from the screen or press zoom-in/zoom-out buttons. The best map name is displayed at the top of the screen. If a folder with OpenStreetMap tiles is detected, then you will see zoom level, x and y coordinates of the center tile. If no maps covers the view area, then no map is used, and the display says “no map”. If there were errors during map download from web or loading from storage card, then the display says “Download Error” or "Read Error". You will also see "Read Error" if there is not enough free memory to display a map (e.g. if an image file is too large, or you try to load too many images at the same time).

Note that you can select a folder with jpeg file or a top folder with OpenStreetMap tiles - i.e. you cannot store both type of maps in the same folder.

10.7 Multi-maps plotting algorithm - for OpenStreetMap tiles use "off" or "multi maps, 1:1 zoom" for best display quality of the map.

If you use multiple jpeg maps, this algorithm tries to plot multiple maps (instead of a single, the best map). The maps are simply plotted on the top of each other, so the trick is to decide in which order to plot the maps. Well, I have an idea to plot the maps in order of "map quality"- starting from the worst, and finishing with the best, so we have the best map on top. There are two factors which determine "map quality" - first is how much area of the screen is covered by the map (the best map will cover 100%). Second factor is what is map zoom level - clearly, there is a zoom level at which the map is best readable on the phone screen - this is selectable in options (you can choose 1x zoom, 2x zoom or 4x zoom). If the zoom level is different (i.e. if you zoom-in or zoom-out) - the map quality will drop (details are too fine and not possible to read, when you zoom-out, or, details dissappear as you zoom in more and more). So the map quality is defined as combination of map coverage and zoom quality factor. Well - this is probably too complicated to explain - but all you need is to experiment with different settings and see which one works best for you. Note that the more maps you use, the more memory is required.

Regarding what set of maps to create - I think the best idea to create maps for the same area at different zoom level (like OpenStreetMap tiles) - e.g. a overview map, then maps which cover some part in more and more details. Then the tool will try to select the best (or the best set of) maps for you. Do not create very large or very small images, the size is about 1000x1000 max.

11. Track-to-follow - Navigation

Use "Track2Follow" button to load a track data from an existing file. You can load data from "gcc", "kml" or "gpx" files - use "left/right" buttons on the open file dialog to see files with different extension. If you have the 1st (main) track (i.e. if you have logging active, or loaded any .gcc files on the "Main" page), then you shall be able to see both tracks plotted on the Maps page - note that you can set line style (color, width and plot as line/dots) for each track separatelly.

You can also enter Track2Follow manually direct in map screen: With context menu switch on "edit t2f (add points)". Then enter t2f click by click in map. With context menu you can "remove last point of t2f" and "undo remove point". Finally switch off edit mode.

To clear "track-to-follow" press "Clear2Follow" button. Then you will see only "main" track plotted on the Maps page.

If you have problems loading file, an error message will be displayed. If you have a KML or GPX file which can be loaded correctly into e.g. GoogleEarth, but cannot be read by this tool, please send me the file, I will try to fix this.

If you have loaded a t2f then you are guided along this track by visual and audible commands (can be switched off). A big arrow on navigation screen or in the right upper corner of map screen always shows the direction you have to drive the next 100m, so you simply can follow this arrow.

You can create your own voice for navigation - see readme.txt in the subdirectory "local".

12. Custom button skins and custom back / fore color

You can set your own images for all buttons. All what required is to edit jpeg files which are supplied with the source code (look into GpsCycleComputerSource.zip). An example of a black skin is supplied in the download area. Create a new subfolder "skin" in the application directory (where GpsCycleComputer.exe resides) and copy the new images in this skin folder, then re-start GpsCycleComputer. The new images will be loaded at the startup. If you want to change just a single button, just copy the files for that button, all files are not required. For example, if you want the change “Maps” button for bottom menu, edit files “map.jpg” and “map_p.jpg” (there are two images, as button has two states, "normal" and "pressed"). Better do not change the image sizes, to avoid image distortions. Hint: to temporarily switch between skins, you can rename the skin folder.

The fore and back color can be changed in Options - Main screen - Select fore/back/mapLabel-color. Take care not to use the same color for fore and back-color...

13. Heart Rate Support

By context menu (or double click) on the main screen in Speed field you can enable heart rate support and display heart rate below the speed. The configuration "speed + hr + signal" is intended for testing and shows also the signal strength. The heart rate can also be viewed in Graph over time or distance.

You need a hardware to feed the pulses of a common heart rate belt into the microphone input of your device. The simplest way is to replace the microphone of a head set with a coil and place it close to the heart rate belt (use the signals strength indicator to find an optimal position). With an amplifier and filter circuit it is possible to get over a distance of 1m. For details see the wiki page at the project site:
http://code.google.com/p/gccv2/wiki/HeartRate.

If you want to make/answer a phone call while using this feature, it may be neccessary not only to unplug the adapter but also to stop the heart rate feature by double click on the speed field.

14. Command Line Interface

You can create a shortcut with a parameter to load a file at startup.
Valid file types are:
- *.dat   for settings
- *.gcc   for tracks
- *.gpx or *.kml   for tracks to follow
The shortcut file is best created on your desktop PC, for example with notepad and then copied to the device. It must have the extension .lnk. It is ASCII encoded and must include a hash (#), the application name and optional a parameter. In front of the hash is the number of characters after the hash (according my experience this number can be omitted.)
To load the settings file "Recall 1.dat" the shortcut file could look like:
101#"\Program Files\GpsCycleComputer\GpsCycleComputer.exe" "\Program Files\GpsCycleComputer\Recall 1.dat"

15. Source code

A complete source code (in C#) is provided. For GCC file format see file Form1.cs, function "LoadGcc" which loads a gcc file. Basically after a header with general data, the data is written as 5 short int (short int = 16 bits = 2 bytes) which are : x, y, z (in metres, relative to starting point), speed (in kmh*10) and time (sec, relative to start). Also there are a few special records (also as 5 short ints) to save some control info, like battery status. Feel free to change anything you like, but please send me you comments/bug fixes.

The project files for MS Visual Studio 2005 are provided. The solution file (*.sln) contains 3 projects: GccGPS (this is the dll to work with GPS - you need to build this for PocketPC 2003 target, release); then GpsCycleComputer project itself (to build the executable - build for Any CPU, release); and the CAB project, the one which creates a CAB (build this last, for Any CPU, release). If you have problems with missing "Platform ID" you can open GpsCycleComputer.csproj in an editor and change the GUID of <PlatformID> to the GUID from a self created project. If you have further problems you could create a project yourself, and just add the existing source files to it. If you have problems building dll - I just use the Wizard to set a MFC DLL project for me (so you could try to do the same), then just add there GccGPS.cpp and .h. If this still does not work, then you actually do not need to build DLL, just copy it from the installation (after you install the application on your phone).

16. Useful Info
16.1 GPS setup

There are many ways for a software to connect to GPS. Usually it is assumed that the GPS hardware is connected to a serial port (e.g. COM0..COM12), so all is requried for a software is to "open" the required serial port and listen to the GPS data. The data comes in a form of text strings in so called NMEA format.

In addition, Windows provides two drivers to connect to GPS (you can read about this here: http://msdn.microsoft.com/en-us/library/bb201942.aspx) - Windows "raw" GPS driver and Windows "parsed" driver. "Raw" driver is also called "multiplexer", as it does not do any processing of the GPS data, but simply forwards it to all applications, i.e. multiple tools can be connected to GPS at the same time. The COM port for the "raw" driver is different from the GPS hardware port, and it is set in Windows "Settings", "System", "External GPS", "Programs" tab - this is usually COM4 (in WinMobile 6.1). The "parsed" driver does more work for you - it converts GPS data into actual latitude/longitude, etc, so you can just retrieve this data without any knowledge of a COM port or NMEA strings generated by GPS.

So how all this can be set in the tool? There is a check box "read GPS data directly", COM port and baud rate selectors on Options page.

Option 1 (default and recommended): Check (i.e. select) "read GPS data directly", set COM port to the multiplexer value (check Windows "Settings", "System", "External GPS", "Programs" tab - usually this is COM4), baud rate does not matter (I think) for multiplexer, set to e.g. 4800. This way you use Windows "raw" driver, and let multiple apps to use GPS at the same time.

Option 2 (very easy, but often GPS does not work well): Un-check (un-select) "read GPS data directly", the COM/baud rate setting are not used in this case. This way you use Window "parsed" driver and let Windows fully manage and process GPS data for you. Note: with this option you cannot use the phone hardware power off button to switch the phone on/off, and still have the GPS logging - you will need to use BkLightOff button - to switch the bklight off.

Option 3: Check (select) "read GPS data directly", set COM port and baud rate to the GPS hardware port setting (you shall have some instructions from the GPS manufacturer). This way you are connected to the GPS directly. Not sure if this will work correctly with multiple GPS applications running at the same time.

You shall be getting at least 1 satellite very quickly (see main screen display, GPS section, parameter "S"), if the settings are correct. If you have 0 satellites or "no gps data" for some time - then something might be wrong in your setup.

Tips for a quick GPS lock: 1) use QuickGPS tool (it is supplied with Diamond); 2) do soft-reset for your phone, specially after software updates; 3) find a place outside building with clear sky view (i.e. do not stay next to a large building wall); 4) do not move your phone, keep it still relative to ground (not relative to your car dashboard while you are driving off !). On Diamond, in this conditions, usually you will get a lock in under 30 sec.

Initialization of GPS Receiver: If you need to initialize your GPS receiver you can place a file "\GccInitGps.txt" in the root directory of your device. If this file exists, its content is sent to the GPS receiver on every open of the GPS.
16.2 Logging raw (NMEA) data from GPS device

By context menu on the main screen in GPS field you can enable "log raw nmea" data. When enabled this logs raw NMEA data from the GPS device and some status information in the file \tmp.txt in the root directory of your device (this option is not stored non volatile; you must switch off and on gps to have changes an effect).

This NMEA data can be useful for evaluation and bug fixing. If the file is renamed to nmea.txt you can choose this file instead of the COM port to use this data as input for the application. So you can test the application indoor with previous recorded GPS data.
16.3 Problems with GoogleMaps installation, if you cannot open KML file

There is a problem with some versions of GoogleMaps for Mobile - it cannot open a KML file as you click on it in your phone File Explorer. You need to edit registry key to fix this. If you do not know how to edit a registry key, then a simple solution would be to install GoogleMaps into main memory - then it shall work. If you have experience with registry, then go to HKEY_CLASSES_ROOT, "kml" key (without . upfront, as there is a key called ".kml"). Inspect the path to your GoogleMaps.exe, it could be wrong - usually the part "Internal Storage" (on UK Windows) is missing - this is what you need to add.

Note that you shall not use any tools to associate kml file with GoogleMaps, as these tools do not know about extra command-line switches (e.g. -KML_FILE) required, i.e. they create incorrect association and you still will not be able to open KML files.
16.4 Updating from older versions to version 4.x

If you are updating from older versions (3.x) to version 4.x it is very likely that you must correct your setting for GPS COM-port. Due to the introduction of COM0 this setting has changed by one step.
In most cases you will get a warning that EOF has reached and default settings are used for the remainder. This is absolutely normal due to new options. Only for the new options default settings are used.

17. License

GpsCycleComputer is Freeware and Open Source.
But you are not allowed to use the code of GpsCycleComputer or pieces of it in any commercial products. You may use it only for personal purposes and other Freeware products.

Copyright (c) 2012, AndyZap, Blaustein
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

18. Contact

Programming: AndyZap, Blaustein 

Design: expo7. Special thanks to AngelGR.

http://forum.xda-developers.com/showthread.php?t=424423

http://code.google.com/p/gccv2/downloads/list

Good luck!
