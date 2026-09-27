// 空实现：odom_echo_lib.hpp 全是 inline 函数；这里只是占位以让 SHARED 库 link 通。
// 真要扩展公共逻辑（比如滤波器、消息转换器）就放这里。

#include "mentorpi_pc_cpp_demo/odom_echo_lib.hpp"

namespace mentorpi_pc_cpp_demo {

int dummy_keep_linker_happy = 0;

}  // namespace mentorpi_pc_cpp_demo