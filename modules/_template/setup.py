from setuptools import setup
import os
from glob import glob

package_name = 'my_cool_algo'   # 改成你的包名

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        ('share/ament_index/resource_index/packages/' + package_name,
         ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='dev',
    maintainer_email='dev@home.local',
    description='My cool algorithm module',
    license='MIT',
    entry_points={
        'console_scripts': [
            # 注册可执行文件，例如：
            # 'my_node = my_cool_algo.my_node:main',
        ],
    },
)