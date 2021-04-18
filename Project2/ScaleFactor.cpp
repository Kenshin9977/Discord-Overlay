#include "ScaleFactor.h"

double GetScaleFactor(HMONITOR monitor)
{
	unsigned int effective_dpiX;
	unsigned int effective_dpiY;
	unsigned int base_dpi = 96;

	GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &effective_dpiX, &effective_dpiY);
	return ((double)effective_dpiX/ (double)base_dpi);

}