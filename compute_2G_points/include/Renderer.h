
#pragma once

#include <functional>
#include <vector>
#include <string>

#include "GL\glew.h"
#include "GLFW\glfw3.h"

#include "imgui.h"
#include "imgui_impl_opengl3.h"
#include "imgui_impl_glfw.h"
#include "implot.h"
#include "implot_internal.h"

#include "glm/common.hpp"
#include "glm/matrix.hpp"
#include <glm/gtx/transform.hpp>

#include "unsuck.hpp"
#include "Debug.h"
#include "OrbitControls.h"
#include "Camera.h"
#include "Box.h"
#include "Framebuffer.h"
#include "Texture.h"



using namespace std;
using glm::dvec3;
using glm::dvec4;
using glm::dmat4;



struct DrawQueue{
	vector<Box> boxes;
	vector<Box> boundingBoxes;

	void clear(){
		boxes.clear();
		boundingBoxes.clear();
	}
};


struct View{
	dmat4 view;
	dmat4 proj;
	shared_ptr<Framebuffer> framebuffer = nullptr;
};

struct GLBuffer {

	GLuint handle = -1;
	int64_t size = 0;

};

struct Renderer {

	GLFWwindow* window = nullptr;
	double fps = 0.0;
	int64_t frameCount = 0;
	
	shared_ptr<Camera> camera = nullptr;
	shared_ptr<OrbitControls> controls = nullptr;

	vector<View> views;
	int width = 0;
	int height = 0;
	DrawQueue drawQueue;

	Renderer();

	void init();
	shared_ptr<Texture> createTexture(int width, int height, GLuint colorType);
	shared_ptr<Framebuffer> createFramebuffer(int width, int height);

	inline GLBuffer createBuffer(int64_t size) {
		GLBuffer buffer {-1, size};
		glCreateBuffers(1, &buffer.handle);
		glNamedBufferStorage(buffer.handle, size, 0, GL_DYNAMIC_STORAGE_BIT | GL_MAP_READ_BIT);
		return buffer;
	}

	inline GLBuffer createSparseBuffer(int64_t size ){
		GLBuffer buffer { -1, size };
		glCreateBuffers(1, &buffer.handle);
		glNamedBufferStorage(buffer.handle, size, 0, GL_DYNAMIC_STORAGE_BIT | GL_SPARSE_STORAGE_BIT_ARB );
		return buffer;
	}

	inline GLBuffer createUniformBuffer(int64_t size) {
		GLBuffer buffer{ -1, size };
		glCreateBuffers(1, &buffer.handle);
		glNamedBufferStorage(buffer.handle, size, 0, GL_DYNAMIC_STORAGE_BIT );
		return buffer;
	}

	void loop(function<void(void)> update, function<void(void)> render);
	void drawBox(glm::dvec3 position, glm::dvec3 scale, glm::ivec3 color);
	void drawBoundingBox(glm::dvec3 position, glm::dvec3 scale, glm::ivec3 color);
	void drawBoundingBoxes(Camera* camera, GLBuffer buffer);
	void drawPoints(void* points, int numPoints);
	void drawPoints(GLuint vao, GLuint vbo, int numPoints);
};