cmake_minimum_required(VERSION 3.20)
project(MyEffect LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 17)

# Point YKS_SDK at the SDK checkout (the folder that contains include/yks/yks_plugin.h).
set(YKS_SDK "${CMAKE_CURRENT_LIST_DIR}/../.." CACHE PATH "YKSPlayer SDK")

add_library(MyEffect SHARED effect.cpp)
target_include_directories(MyEffect PRIVATE ${YKS_SDK}/include)
set_target_properties(MyEffect PROPERTIES MSVC_RUNTIME_LIBRARY "MultiThreaded")  # no VC++ redistributable needed
if(MSVC)
  target_compile_options(MyEffect PRIVATE /utf-8 /W4)
endif()
add_custom_command(TARGET MyEffect POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different ${CMAKE_CURRENT_LIST_DIR}/yks-plugin.json $<TARGET_FILE_DIR:MyEffect>)
