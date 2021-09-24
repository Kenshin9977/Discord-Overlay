//-----------------------------------------------------------------------------
// Includes
//-----------------------------------------------------------------------------
#pragma region

#include <SDKDDKVer.h>
#include "exception\exception.hpp"
#include "logging\dump.hpp"
#include "ui\window.hpp"
#include "ui\window.hpp"
#include "ScaleFactor.h"

#pragma endregion

//-----------------------------------------------------------------------------
// System Includes
//-----------------------------------------------------------------------------
#pragma region
#include <chrono>
#include <thread>

#include <d3d11.h>
#include <dxgi1_2.h>

#pragma endregion

//-----------------------------------------------------------------------------
// Linker Directives
//-----------------------------------------------------------------------------
#pragma region

#pragma comment (lib, "d3d11.lib")
#pragma comment (lib, "dxgi.lib")

#pragma endregion

//-----------------------------------------------------------------------------
// Integrated + Dedicated GPU on notebooks
//-----------------------------------------------------------------------------
#pragma region

/**
 NVIDIA Optimus enablement
 @pre NVIDIA Control Panel > Preferred graphics processor > "Auto-select"
 */
extern "C" {
	__declspec(dllexport) DWORD NvOptimusEnablement = 0x00000000;
}

/**
 AMD "Optimus" enablement
 */
extern "C" {
	__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
}

#pragma endregion

//-----------------------------------------------------------------------------
// Declarations and Definitions
//-----------------------------------------------------------------------------

using namespace mage;

namespace {

	UniquePtr< Window > g_window;

	constexpr D3D_DRIVER_TYPE g_driver_types[] = {
		D3D_DRIVER_TYPE_HARDWARE,
		D3D_DRIVER_TYPE_WARP,
		D3D_DRIVER_TYPE_REFERENCE,
	};
	constexpr D3D_FEATURE_LEVEL g_feature_levels[] = {
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0
	};
	D3D_DRIVER_TYPE g_driver_type;
	D3D_FEATURE_LEVEL g_feature_level;
	ComPtr< ID3D11Device > g_device;
	ComPtr< ID3D11DeviceContext > g_device_context;
	ComPtr< IDXGISwapChain1 > g_swap_chain;
	ComPtr< ID3D11RenderTargetView > g_rtv;

	void Init(NotNull< HINSTANCE > instance, U32x2 g_display_resolution) {
		#ifdef _DEBUG
		const int debug_flags = _CrtSetDbgFlag(_CRTDBG_REPORT_FLAG);
		// Perform automatic leak checking at program exit through a call to
		// _CrtDumpMemoryLeaks and generate an error report if the application
		// failed to free all the memory it allocated.
		_CrtSetDbgFlag(debug_flags | _CRTDBG_LEAK_CHECK_DF);
		#endif

		// Add filter for unhandled exceptions.
		AddUnhandledExceptionFilter();

		// Initialize a console.
		//InitializeConsole();
		//Print("Copyright (c) 2016-2019 Matthias Moulin.\n");

		// Initialize a window.
		{
			auto window_desc = MakeShared< WindowDescriptor >(instance, L"Discord Overlay");
			g_window = MakeUnique< Window >(std::move(window_desc),
											L"Discord Overlay",
											g_display_resolution);
		}

		// Initialize the device and device context.
		{
			#ifdef NDEBUG
			constexpr U32 create_device_flags = 0u;
			#else  // NDEBUG
			constexpr U32 create_device_flags = D3D11_CREATE_DEVICE_DEBUG;
			#endif // NDEBUG

			// Get the ID3D11Device and ID3D11DeviceContext.
			HRESULT result = E_FAIL;
			for (const auto driver_type : g_driver_types) {
				g_driver_type = driver_type;
				result = D3D11CreateDevice(nullptr,
										   g_driver_type,
										   nullptr,
										   create_device_flags,
										   g_feature_levels,
										   static_cast< U32 >(std::size(g_feature_levels)),
										   D3D11_SDK_VERSION,
										   g_device.ReleaseAndGetAddressOf(),
										   &g_feature_level,
										   g_device_context.ReleaseAndGetAddressOf());
				if (SUCCEEDED(result)) {
					break;
				}
			}
			ThrowIfFailed(result, "ID3D11Device creation failed: {:08X}.", result);
		}
	
		// Initialize the swap chain.
		{
			// Get the IDXGIDevice2.
			ComPtr< IDXGIDevice2 > dxgi_device;
			{
				const HRESULT result = g_device->QueryInterface(__uuidof(IDXGIDevice2), 
					                                            (void**)dxgi_device.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "IDXGIDevice2 creation failed: {:08X}.", result);
			}

			// Get the IDXGIAdapter.
			ComPtr< IDXGIAdapter > dxgi_adapter;
			{
				const HRESULT result = dxgi_device->GetAdapter(dxgi_adapter.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "IDXGIAdapter creation failed: {:08X}.", result);
			}

			// Get the IDXGIFactory2.
			ComPtr< IDXGIFactory2 > dxgi_factory;
			{
				const HRESULT result = dxgi_adapter->GetParent(__uuidof(IDXGIFactory2),
					                                           (void**)dxgi_factory.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "IDXGIFactory2 creation failed: {:08X}.", result);
			}

			dxgi_factory->MakeWindowAssociation(g_window->GetWindow(),
												DXGI_MWA_NO_WINDOW_CHANGES
			                                  | DXGI_MWA_NO_ALT_ENTER
			                                  | DXGI_MWA_NO_PRINT_SCREEN);

			// Create the swap chain descriptor.
			DXGI_SWAP_CHAIN_DESC1 swap_chain_desc = {};
			swap_chain_desc.Width       = g_display_resolution[0u];
			swap_chain_desc.Height      = g_display_resolution[1u];
			swap_chain_desc.Format      = DXGI_FORMAT_R8G8B8A8_UNORM;
			swap_chain_desc.SampleDesc.Count = 1u;
			swap_chain_desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
			swap_chain_desc.BufferCount = 1u;
			swap_chain_desc.AlphaMode   = DXGI_ALPHA_MODE_IGNORE;
			swap_chain_desc.Flags       = DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH;
			
			// Create the swap chain.
			{
				const HRESULT result = dxgi_factory->CreateSwapChainForHwnd(g_device.Get(),
																			g_window->GetWindow(),
																			&swap_chain_desc,
																			nullptr,
																			nullptr,
																			g_swap_chain.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "IDXGISwapChain creation failed: {:08X}.", result);
			}
		
			g_swap_chain->SetFullscreenState(FALSE, nullptr);
		}
	
		// Initialize the RTV.
		{
			// Get the back buffer.
			ComPtr< ID3D11Texture2D > back_buffer;
			{
				const HRESULT result = g_swap_chain->GetBuffer(0u,
															   __uuidof(ID3D11Texture2D),
															   (void**)back_buffer.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "Back buffer texture creation failed: {:08X}.", result);
			}

			// Create the RTV.
			{
				// Create the RTV.
				const HRESULT result = g_device->CreateRenderTargetView(back_buffer.Get(),
																		nullptr,
																		g_rtv.ReleaseAndGetAddressOf());
				ThrowIfFailed(result, "Back buffer RTV creation failed: {:08X}.", result);
			}
		
			// Bind the RTV.
			g_device_context->OMSetRenderTargets(1u, g_rtv.GetAddressOf(), nullptr);
		}
	
		// Initialize the viewport.
		{
			// Create the viewport.
			D3D11_VIEWPORT viewport;
			viewport.TopLeftX = 0.0f;
			viewport.TopLeftY = 0.0f;
			viewport.Width    = static_cast< F32 >(g_display_resolution[0u]);
			viewport.Height   = static_cast< F32 >(g_display_resolution[1u]);
			viewport.MinDepth = 0.0f;
			viewport.MaxDepth = 1.0f;

			// Bind the viewport.
			g_device_context->RSSetViewports(1u, &viewport);
		}
	}

	void Uninit() noexcept {
		// Switch to windowed mode since Direct3D is incapable of when in 
		// fullscreen mode due to certain threading issues that occur behind
		// the scenes.
		if (g_swap_chain) {
			g_swap_chain->SetFullscreenState(FALSE, nullptr);
		}

		// Reset any device context to the default settings.
		if (g_device_context) {
			g_device_context->ClearState();
		}
	}
	
	void Render() {
		static constexpr F32x4 s_background_color = {
			46/255.0f, 49/255.0f, 54/255.0f, 1.0f
		}; //Old overylay's color

		// Clear the back buffer.
		g_device_context->ClearRenderTargetView(g_rtv.Get(), s_background_color.data());

		// Present the back buffer to the front buffer.
		g_swap_chain->Present(0u, 0u);
	}

	[[nodiscard]]
	int Run(int nCmdShow) {
		// Show the main window.
		g_window->Show(nCmdShow);

		// Enter the message loop.
		MSG msg;
		SecureZeroMemory(&msg, sizeof(msg));
		while (WM_QUIT != msg.message) {

			// Retrieves messages for any window that belongs to the current
			// thread without performing range filtering. Furthermore messages
			// are removed after processing.
			if (PeekMessage(&msg, nullptr, 0u, 0u, PM_REMOVE)) {
				// Translates virtual-key messages into character messages.
				TranslateMessage(&msg);
				// Dispatches a message to a window procedure.
				DispatchMessage(&msg);
				continue;
			}

			Render();
			std::this_thread::sleep_for(std::chrono::milliseconds(34));

		}

		Uninit();
		
		return static_cast< int >(msg.wParam);
	}
}


int WINAPI WinMain(_In_ HINSTANCE instance,
				   _In_opt_ [[maybe_unused]] HINSTANCE prev_instance,
				   _In_     [[maybe_unused]] LPSTR lpCmdLine,
				   _In_ int nCmdShow) {
	MonitorRects22 monitors;
	double windowScaleFactor = 1;
	for (int i = 0; i < monitors.hMonitors.size(); i++)
	{
		windowScaleFactor *= GetScaleFactor(monitors.hMonitors[i]);
	}
	
	windowScaleFactor = (double)(int)(windowScaleFactor * 100 + .5) / 100; //Limit the precision of the scale factor to double digit decimal
	Init(NotNull< HINSTANCE >(instance), {UINT32(windowScaleFactor*769), UINT32(windowScaleFactor *433)});
	return Run(nCmdShow);
}
