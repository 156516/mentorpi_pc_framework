import os
from glob import glob
from setuptools import setup

package_name = 'obstacle_distance'

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        ('share/' + package_name, ['package.xml']),
        (os.path.join('share', package_name, 'launch'),
         glob('launch/*.py')),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='dev',
    maintainer_email='dev@home.local',
    description='Obstacle distance algorithm module (front/left/right min)',
    license='MIT',
    entry_points={
        'console_scripts': [
            'obstacle_node = obstacle_distance.obstacle_node:main',
        ],
    },
)