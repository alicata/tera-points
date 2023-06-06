# tera-points
Research into billion/trillion point renderer from depth / LiDAR sensors data

## goal
Create cleaner and easier to hack research source code in GPU accelerated sensor point cloud rendering.

## additonal credit
The starting point of this code base is a rewrite of the original https://arxiv.org/abs/2204.01287. 
Sub-project compute_2G_points is focused on billion points, but future version might target trillion points.

## description
A GPU compute shader rasterization pipeline for point clouds that can render up to two billion points in real-time at 60fps.
The improvements are achieved by batching points in a way that a number of batch-level optimizations can be computed before rasterizing the points within the same rendering pass.

This approach is suitable for rendering arbitrarily large point clouds.

The pipeline also supports frustum culling and LOD rendering.










