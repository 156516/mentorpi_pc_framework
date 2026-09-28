from setuptools import setup
import os
from glob import glob

package_name = 'mentorpi_pc_slam_toolbox'

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        # ⚠️ 不要 explicit install ament marker——
        # setuptools develop 模式跟 colcon --symlink-install 会冲突（"File exists"）
        # 跟 modules/_template 一致，靠 colcon 隐式 fallback 注册
        ('share/' + package_name, ['package.xml']),
        # config / launch 由 Dockerfile build 完后手动 COPY 到 install 端
        # （不能用 data_files glob——会和 --symlink-install 冲突）
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='dev',
    maintainer_email='dev@home.local',
    description='PC-side slam_toolbox mapper for MentorPi',
    license='MIT',
)

