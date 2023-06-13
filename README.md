# Tera-points
Research into accellerated billion-to-trillion point renderer from 3d sensor or synthetic sample data.

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
* load from large file 

## Credit
The starting code is based on agressive simplication (for easy experimentation) modifications from the original paper and code "Software Rasterization of 2 Billion Points in Real Time" https://arxiv.org/abs/2204.01287. 
 
## Future Plans
Sub-project compute_2G_points is focused on billion points, but future version will target trillion points.







