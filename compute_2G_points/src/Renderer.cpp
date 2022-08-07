
#include <filesystem>

#include "Renderer.h"

#include "drawBoundingBoxes.h"
#include "drawBoundingBoxesIndirect.h"
#include "drawBoxes.h"
#include "drawPoints.h"
#include "Runtime.h"

namespace fs = std::filesystem;

auto _controls = make_shared<OrbitControls>();

static void APIENTRY debugCallback(GLenum source, GLenum type, GLuint id, GLenum severity, GLsizei length, const GLchar* message, const void* userParam) {
	if (
		severity == GL_DEBUG_SEVERITY_NOTIFICATION 
		|| severity == GL_DEBUG_SEVERITY_LOW 
		|| severity == GL_DEBUG_SEVERITY_MEDIUM
		) {
		return;
	}
	cout << "OPENGL DEBUG CALLBACK: " << message << endl;
}

void error_callback(int error, const char* description){
	fprintf(stderr, "Error: %s\n", description);
}

static void key_callback(GLFWwindow* window, int key, int scancode, int action, int mods){
	cout << "key: " << key << ", scancode: " << scancode << ", action: " << action << ", mods: " << mods << endl;

	if (key == GLFW_KEY_ESCAPE && action == GLFW_PRESS) {
		glfwSetWindowShouldClose(window, GLFW_TRUE);
	}
	Runtime::keyStates[key] = action;
	cout << key << endl;
}

static void cursor_position_callback(GLFWwindow* window, double xpos, double ypos){
	ImGuiIO& io = ImGui::GetIO();
	if(io.WantCaptureMouse){
		return;
	}
	
	Runtime::mousePosition = {xpos, ypos};
	_controls->onMouseMove(xpos, ypos);
}

void scroll_callback(GLFWwindow* window, double xoffset, double yoffset){
	ImGuiIO& io = ImGui::GetIO();
	if(io.WantCaptureMouse){
		return;
	}
	_controls->onMouseScroll(xoffset, yoffset);
}


void mouse_button_callback(GLFWwindow* window, int button, int action, int mods){
	cout << "start button: " << button << ", action: " << action << ", mods: " << mods << endl;

	ImGuiIO& io = ImGui::GetIO();
	if(io.WantCaptureMouse){
		return;
	}
	cout << "end button: " << button << ", action: " << action << ", mods: " << mods << endl;

	if(action == 1){
		Runtime::mouseButtons = Runtime::mouseButtons | (1 << button);
	}else if(action == 0){
		uint32_t mask = ~(1 << button);
		Runtime::mouseButtons = Runtime::mouseButtons & mask;
	}
	_controls->onMouseButton(button, action, mods);
}

Renderer::Renderer(){
	this->controls = _controls;
	camera = make_shared<Camera>();

	init();
	View view1;
	view1.framebuffer = this->createFramebuffer(128, 128);
	View view2;
	view2.framebuffer = this->createFramebuffer(128, 128);
	views.push_back(view1);
	views.push_back(view2);
}

void init_gui(Renderer* renderer) {
	{ // SETUP IMGUI
		IMGUI_CHECKVERSION();
		ImGui::CreateContext();
		ImPlot::CreateContext();
		ImGuiIO& io = ImGui::GetIO();
		ImGui_ImplGlfw_InitForOpenGL(renderer->window, true);
		ImGui_ImplOpenGL3_Init("#version 450");
		ImGui::StyleColorsDark();
	}
}

void destroy_gui(void) {
	ImPlot::DestroyContext();
	ImGui::DestroyContext();
}


void Renderer::init(){
	glfwSetErrorCallback(error_callback);

	if (!glfwInit()) {
		assert(!"glfw init failed.");
	}

	glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 4);
	glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 6);
	glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
	glfwWindowHint(GLFW_DECORATED, true);

	int numMonitors;
	GLFWmonitor** monitors = glfwGetMonitors(&numMonitors);

	cout << "<create windows>" << endl;
	{
		const GLFWvidmode * mode = glfwGetVideoMode(monitors[0]);

		window = glfwCreateWindow(mode->width - 100, mode->height - 100, "Point Cloud GPU Renderer - Compute Rasterizer", nullptr, nullptr);
		if (!window) {
			glfwTerminate();
			exit(EXIT_FAILURE);
		}
		glfwSetWindowPos(window, 50, 50);
	}

	cout << "<set input callbacks>" << endl;
	glfwSetKeyCallback(window, key_callback);
	glfwSetCursorPosCallback(window, cursor_position_callback);
	glfwSetMouseButtonCallback(window, mouse_button_callback);
	glfwSetScrollCallback(window, scroll_callback);

	glfwMakeContextCurrent(window);
	glfwSwapInterval(0);

	GLenum err = glewInit();
	if (GLEW_OK != err) {
		/* Problem: glewInit failed, something is seriously wrong. */
		fprintf(stderr, "glew error: %s\n", glewGetErrorString(err));
	}

	cout << "<glewInit done> " << "(" << now() << ")" << endl;

	glEnable(GL_DEBUG_OUTPUT);
	glEnable(GL_DEBUG_OUTPUT_SYNCHRONOUS);
	glDebugMessageControl(GL_DONT_CARE, GL_DONT_CARE, GL_DEBUG_SEVERITY_HIGH, 0, NULL, GL_TRUE);
	glDebugMessageCallback(debugCallback, NULL);

	init_gui(this);
}

shared_ptr<Texture> Renderer::createTexture(int width, int height, GLuint colorType) {
	auto texture = Texture::create(width, height, colorType, this);
	return texture;
}

shared_ptr<Framebuffer> Renderer::createFramebuffer(int width, int height) {
	auto framebuffer = Framebuffer::create(this);
	GLenum status = glCheckNamedFramebufferStatus(framebuffer->handle, GL_FRAMEBUFFER);

	if (status != GL_FRAMEBUFFER_COMPLETE) {
		cout << "framebuffer incomplete" << endl;
	}
	return framebuffer;
}

void render_gui(Renderer * renderer) {
	// IMGUI
	ImGui_ImplOpenGL3_NewFrame();
	ImGui_ImplGlfw_NewFrame();
	ImGui::NewFrame();

	shared_ptr<OrbitControls> controls = renderer->controls;
	vector<View>& views = renderer->views;

	{ // RENDER IMGUI INPUT
		auto windowSize_debug = ImVec2(490, 200);
		ImGui::SetNextWindowPos(ImVec2(
			10,
			30));
		ImGui::SetNextWindowSize(windowSize_debug);

		ImGui::Begin("Debug Visualization");
		{
			bool checked;


			checked = Debug::showBoundingBox;
			ImGui::Checkbox("show bounding box", &checked);
			Debug::showBoundingBox = checked;

			checked = Debug::colorizeChunks;
			ImGui::Checkbox("colorize chunks", &checked);
			Debug::colorizeChunks = checked;
		}

		if (ImGui::Button("copy camera")) {
			auto pos = controls->getPosition();
			auto target = controls->target;

			stringstream ss;
			ss << std::setprecision(2) << std::fixed;
			ss << "position: " << pos.x << ", " << pos.y << ", " << pos.z << endl;
			ss << "renderer->controls->yaw = " << controls->yaw << ";" << endl;
			ss << "renderer->controls->pitch = " << controls->pitch << ";" << endl;
			ss << "renderer->controls->radius = " << controls->radius << ";" << endl;
			ss << "renderer->controls->target = {" << target.x << ", " << target.y << ", " << target.z << "};" << endl;

			string str = ss.str();
			toClipboard(str);
		}

		if (ImGui::Button("reset view")) {
			Debug::requestResetView = true;
		}
		ImGui::End();
	}

	{
		auto source = views[0].framebuffer;
		glBlitNamedFramebuffer(
			source->handle, 0,
			0, 0, source->width, source->height,
			0, 0, 0 + source->width, 0 + source->height,
			GL_COLOR_BUFFER_BIT, GL_LINEAR);
	}

	ImGui::Render();
	ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());


}

void Renderer::loop(function<void(void)> update, function<void(void)> render){

	while (!glfwWindowShouldClose(window)){
		Debug::clearFrameStats();

		// WINDOW
		int width, height;
		glfwGetWindowSize(window, &width, &height);
		camera->setSize(width, height);
		this->width = width;
		this->height = height;

		EventQueue::instance->process();

		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		glViewport(0, 0, this->width, this->height);
		{ 
			controls->update();
			camera->world = controls->world;
			camera->position = camera->world * dvec4(0.0, 0.0, 0.0, 1.0);
			drawQueue.clear();
		}

		{ // UPDATE & RENDER
			camera->update();
			update();
			camera->update();

			glClearColor(0.0f, 0.2f, 0.3f, 1.0f);
			glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
			glEnable(GL_DEPTH_TEST);

			render();
			{
				auto& view = this->views[0];
				glBindFramebuffer(GL_FRAMEBUFFER, view.framebuffer->handle);
				_drawBoundingBoxes(camera.get(), drawQueue.boundingBoxes);
				_drawBoxes(camera.get(), drawQueue.boxes);
			}
			glBindFramebuffer(GL_FRAMEBUFFER, 0);
			glViewport(0, 0, this->width, this->height);
		}
		render_gui(this);

		glfwSwapBuffers(window);
		glfwPollEvents();
	}

	destroy_gui();

	glfwDestroyWindow(window);
	glfwTerminate();
	exit(EXIT_SUCCESS);

}

void Renderer::drawBox(glm::dvec3 position, glm::dvec3 scale, glm::ivec3 color){
	Box box;
	box.min = position - scale / 2.0;
	box.max = position + scale / 2.0;;
	box.color = color;
	drawQueue.boxes.push_back(box);
}

void Renderer::drawBoundingBox(glm::dvec3 position, glm::dvec3 scale, glm::ivec3 color){
	Box box;
	box.min = position - scale / 2.0;
	box.max = position + scale / 2.0;;
	box.color = color;
	drawQueue.boundingBoxes.push_back(box);
}

void Renderer::drawBoundingBoxes(Camera* camera, GLBuffer buffer){
	_drawBoundingBoxesIndirect(camera, buffer);
}

void Renderer::drawPoints(void* points, int numPoints){
	_drawPoints(camera.get(), points, numPoints);
}

void Renderer::drawPoints(GLuint vao, GLuint vbo, int numPoints) {
	_drawPoints(camera.get(), vao, vbo, numPoints);
}


