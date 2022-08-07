/*
    1. select Release x64
	2. build solution
	3. run Local Window Debugger
	4. drag n drop test.las

	TODO:
	1. add CUDA
	2. add optimized rendering module
	3. add VR
	4. add Debug tool
	5. add timer

*/

#include <iostream>
#include <filesystem>

#include "Renderer.h"
#include "Shader.h"
#include "ProgressiveFileBuffer.h"

#include "cudaGL.h"
#include "Runtime.h"

#include "data/point_clouds_loader.h"
#include "compute/compute_loop.h"



using namespace std;

int numPoints = 1'000'000;

void init_cuda(void) {
	// Creating a CUDA context
	cuInit(0);
	static CUdevice cuDevice;
	static CUcontext context;
	cuDeviceGet(&cuDevice, 0);
	cuCtxCreate(&context, 0, cuDevice);
}

void init_debug(void) {
	Debug::frustumCullingEnabled = true;
	Debug::updateFrustum = true;
	Debug::enableShaderDebugValue = false;
	Debug::updateEnabled = true;
	Debug::colorizeOverdraw = true;
	Debug::showBoundingBox = true;
	Debug::colorizeChunks = true;
}

shared_ptr<PointCloudLoader> load_point_clouds(shared_ptr<Renderer> renderer) {
	auto point_clouds = make_shared<PointCloudLoader>(renderer);
	vector<string> lasfiles = { "d:\\data\\test.las" };

	point_clouds->add(lasfiles, [renderer](vector<shared_ptr<PointCloud>> lasfiles) {} );
	return point_clouds;
}

void update_compute_loop(shared_ptr<Renderer> renderer) {
	auto selected = Runtime::getSelectedMethod();
	if (selected) {

		selected->update(renderer.get());
	}
}

int main(){

	cout << std::setprecision(2) << std::fixed;
	init_cuda();
	auto renderer = make_shared<Renderer>();

	auto tStart = now();

	// load point clouds from file to GPU memory->isSelected
	auto pointclouds = load_point_clouds(renderer);
	// 4-4-4 byte format
	Runtime::pointclouds_loader = pointclouds;
	Runtime::addMethod((Method*)new ComputeLoop(renderer.get(), pointclouds));
	Runtime::setSelectedMethod(Runtime::methods[0]->name);
 
	init_debug();


	glfwFocusWindow(renderer->window);
	bool firstTime = true;

	auto points_update = [&](){

		pointclouds->process();
		update_compute_loop(renderer);
				
		for(auto pc : Runtime::pointclouds_loader->files){
			if (firstTime) 
			{
				firstTime = false;

				auto size = pc->boxMax - pc->boxMin;
				auto position = (pc->boxMax + pc->boxMin) / 2.0;
				auto radius = glm::length(size) / 1.5;

				renderer->controls->yaw = 0.53;
				renderer->controls->pitch = -0.68;
				renderer->controls->radius = radius;
				renderer->controls->target = position;
			}
		}
	};

	auto render = [&](){
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		glClearColor(0.0, 0.0, 0.0, 1.0);
		glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

		{
			auto& view = renderer->views[0];
			view.view = renderer->camera->view;
			view.proj = renderer->camera->proj;
			renderer->views[0].framebuffer->setSize(renderer->width, renderer->height);

			glBindFramebuffer(GL_FRAMEBUFFER, view.framebuffer->handle);
			glClearColor(0.0, 0.2, 0.3, 1.0);
			glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

			glBindFramebuffer(GL_FRAMEBUFFER, 0);
			Runtime::getSelectedMethod()->render(renderer.get());
		}
		
		for(auto pc : Runtime::pointclouds_loader->files){
			dvec3 size = pc->boxMax - pc->boxMin;
			dvec3 position = (pc->boxMax + pc->boxMin) / 2.0;
			renderer->drawBoundingBox(position, size, {255, 255, 0});
		}
	};

	renderer->loop(points_update, render);
	return 0;
}

