# Tera-points
Research into accellerated billion-to-trillion point renderer from 3d sensor or synthetic sample data.

![2G](https://github.com/alicata/tera-points/blob/main/2G_Renderer.png)

## Overview
This code project implements a real-time GPU rasterization system for point clouds up to 100 times more performant than GL_POINTS, achieved through optimizations such as utilizing 
* compute shader
* grouping points into batches
* employing adaptive precision techniques
	
## Principle
1. The compute shader transforms points into screen space, each pixel has depth and color information packed into a single 64-bit integer.
2. Use AtomicMin to determine the closest point for each pixel.

## What's good for
Some of the features of the current code:
* rendering arbitrarily large point clouds.
* load from large file (LAS)

## Setup
* Install NVDIA CUDA Toolkit
* Install VisualStudio 2022 (C++)
* Navigate to bin and open Compute_2G_Points.sln
* Build solution (Release, x64)
* Place and rename a LAS file in path tera_points/test.las

## Credit
The starting code is based on agressive simplication (for easy experimentation) modifications from the original paper and code "Software Rasterization of 2 Billion Points in Real Time" https://arxiv.org/abs/2204.01287. 
 
## Future Plans
* expand compute_2G_points from 2 billion points to handle trillion points (using 4GB GPU VRAM)
* load from .obj wavefront and other common fileformats
* allow accumulating data loaded from separate files.
*

