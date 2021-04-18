#ifndef SCALE_FACTOR_
#define SCALE_FACTOR_

#pragma once

#include <Windows.h>
#include <vector>
#include <ShellScalingApi.h>

#pragma comment(lib, "Shcore.lib")

struct MonitorRects22
{
	std::vector<RECT>   rcMonitors = std::vector<RECT> ();
	std::vector<HMONITOR>   hMonitors = std::vector<HMONITOR>();
	//double windowScaleFactor = 1;

	static BOOL CALLBACK MonitorEnum(HMONITOR hMon, HDC hdc, LPRECT lprcMonitor, LPARAM pData)
	{
		
		MonitorRects22* pThis = reinterpret_cast<MonitorRects22*>(pData);
		MonitorRects22* phThis = reinterpret_cast<MonitorRects22*>(pData);
		pThis->rcMonitors.push_back(*lprcMonitor);
		phThis->hMonitors.push_back(hMon);
		return TRUE;
	}

	MonitorRects22()
	{
		EnumDisplayMonitors(0, 0, MonitorEnum, (LPARAM)this);
	}
};

double GetScaleFactor(HMONITOR monitor);

#endif SCALE_FACTOR_