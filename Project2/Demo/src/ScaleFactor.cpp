//#include "ScaleFactor.h"

#include <Windows.h>
#include <vector>

//extern double windowScaleFactor;
extern std::vector<HMONITOR>   hMonitors;

//std::vector<HMONITOR>   hMonitors;
double windowScaleFactor = 1;

struct MonitorRects22
{
	std::vector<RECT> rcMonitors;

	static BOOL CALLBACK MonitorEnum(HMONITOR hMon, HDC hdc, LPRECT lprcMonitor, LPARAM pData)
	{
		hMonitors.push_back(hMon);
		MonitorRects22* pThis = reinterpret_cast<MonitorRects22*>(pData);
		pThis->rcMonitors.push_back(*lprcMonitor);
		return TRUE;
	}

	MonitorRects22()
	{
		EnumDisplayMonitors(0, 0, MonitorEnum, (LPARAM)this);
	}
};

void GetScaleFactor(HMONITOR monitor)
{
	MONITORINFOEX monitorInfoEx;
	monitorInfoEx.cbSize = sizeof(monitorInfoEx);
	GetMonitorInfo(monitor, &monitorInfoEx);
	auto cxLogical = monitorInfoEx.rcMonitor.right - monitorInfoEx.rcMonitor.left;
	auto cyLogical = monitorInfoEx.rcMonitor.bottom - monitorInfoEx.rcMonitor.top;


	DEVMODE devMode;
	devMode.dmSize = sizeof(devMode);
	devMode.dmDriverExtra = 0;
	EnumDisplaySettings(monitorInfoEx.szDevice, ENUM_CURRENT_SETTINGS, &devMode);
	auto cxPhysical = devMode.dmPelsWidth;
	auto cyPhysical = devMode.dmPelsHeight;

	auto horizontalScale = ((double)cxPhysical / (double)cxLogical);
	auto verticalScale = ((double)cyPhysical / (double)cyLogical);

	windowScaleFactor *= horizontalScale;
}