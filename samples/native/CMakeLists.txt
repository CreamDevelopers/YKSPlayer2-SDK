# Native sample plugins. Builds standalone (cmake -S sdk/samples/native) or inside the YKSPlayer tree.
cmake_minimum_required(VERSION 3.20)
project(yks_native_samples LANGUAGES C CXX)

set(YKS_SDK_INCLUDE ${CMAKE_CURRENT_LIST_DIR}/../../include CACHE INTERNAL "SDK headers")
if(NOT DEFINED YKS_PLUGIN_OUTPUT_DIR)
  set(YKS_PLUGIN_OUTPUT_DIR ${CMAKE_BINARY_DIR}/plugins)
endif()

# yks_add_plugin(<target> <sources...>) — a DLL exporting yks_plugin_query, static CRT, in plugins/<target>/.
function(yks_add_plugin target)
  add_library(${target} SHARED ${ARGN})
  target_include_directories(${target} PRIVATE ${YKS_SDK_INCLUDE})
  set_target_properties(${target} PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    RUNTIME_OUTPUT_DIRECTORY ${YKS_PLUGIN_OUTPUT_DIR}/${target}
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${YKS_PLUGIN_OUTPUT_DIR}/${target}
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${YKS_PLUGIN_OUTPUT_DIR}/${target}
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO ${YKS_PLUGIN_OUTPUT_DIR}/${target})
endfunction()

yks_add_plugin(sine-decoder sine-decoder/sine_decoder.c)
yks_add_plugin(bitcrusher bitcrusher/bitcrusher.cpp)
target_compile_features(bitcrusher PRIVATE cxx_std_17)

foreach(p sine-decoder bitcrusher)
  configure_file(${CMAKE_CURRENT_LIST_DIR}/${p}/yks-plugin.json ${YKS_PLUGIN_OUTPUT_DIR}/${p}/yks-plugin.json COPYONLY)
endforeach()
