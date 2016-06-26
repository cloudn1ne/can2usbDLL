# can2usbDLL
can2usbAVR interface to vb.net

contains the can2usb.DLL which is the interface DLL to the Arduino/CAN Shield + can2usbAVR code, as well as a little VB.net test project,
that can be used to connect to a KLINE/CAN T4e and discover the access level.

KLINE+CAN ECUs (earlier MY's)
-----------------------------
* select 1000kbit CAN speed
* KLINE+CAN ECU's should be freely readable, used the "Download ECU" function, specifcy a folder and you should be able to get the interessting parts of the ECU downloaded

CAN ECU's (later MY's)
----------------------
* select 500kbit CAN speed
* CAN  ECU's are locked down and can not be read out without prior flashing to enable those memory functions, however datalogging and OBD should work.

Tests
-----
* Burst 0x80 - tests the datalogger function, ECU will reply with burst data containing the variables. This should work on CAN+KLINE and CAN ECU's
* Memory Read 0x50 - reads first 32 byte of calibration. This should work on CAN+KLINE ECU and previously cracked CAN ECU's
* OBD Read - issues a Mode 0x22 (Enhanced Vehicle Data) call via CAN/OBD. This will not work on CAN+KLINE ECU's because they support OBD only via CAN. Should work fine on any CAN ECU (T6e as well)

Download ECU
------------
Works on CAN+KLINE or cracked CAN ECU's.
Will download bootloader, flash calibration (calrom), calibration live data (calram), ECU program, and DECRAM (Non volatile memory/settings)

