import os
from glob import glob
from setuptools import setup

package_name = 'mentorpi_pc_monitor'

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        ('share/' + package_name, ['package.xml']),
        # 任何 .py launch 文件：被 ament 自动发现
        (os.path.join('share', package_name, 'launch'),
         glob('launch/*.py')),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='robot',
    maintainer_email='robot@home.local',
    description='PC 端 MentorPi 监控节点',
    license='MIT',
    tests_require=['pytest'],
    entry_points={
        'console_scripts': [
            # 注意：main() 必须接受 rclpy 风格的 args
            'monitor_node = mentorpi_pc_monitor.monitor_node:main',
            'dashboard_node = mentorpi_pc_monitor.dashboard_node:main',
        ],
    },
)