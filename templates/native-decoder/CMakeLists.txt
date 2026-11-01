cmake_minimum_required(VERSION 3.20)
project(MyDecoder LANGUAGES C)

set(YKS_SDK "${CMAKE_CURRENT_LIST_DIR}/../.." CACHE PATH "YKSPlayer SDK")

add_library(MyDecoder SHARED decoder.c)
target_include_directories(MyDecoder PRIVATE ${YKS_SDK}/include)
set_target_properties(MyDecoder PROPERTIES MSVC_RUNTIME_LIBRARY "MultiThreaded")
if(MSVC)
  target_compile_options(MyDecoder PRIVATE /utf-8 /W4)
endif()
add_custom_command(TARGET MyDecoder POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different ${CMAKE_CURRENT_LIST_DIR}/yks-plugin.json $<TARGET_FILE_DIR:MyDecoder>)
