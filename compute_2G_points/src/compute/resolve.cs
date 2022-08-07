#version 450

#extension GL_ARB_gpu_shader_int64 : enable
#extension GL_NV_shader_atomic_int64 : enable
#extension GL_NV_gpu_shader5 : enable
#extension GL_KHR_shader_subgroup_basic : require
#extension GL_KHR_shader_subgroup_arithmetic : require
#extension GL_KHR_shader_subgroup_ballot : require
#extension GL_KHR_shader_subgroup_vote : require
#extension GL_KHR_shader_subgroup_clustered : require

layout(local_size_x = 16, local_size_y = 16) in;

layout(r32ui , binding =  0) coherent uniform uimage2D uOutput;
layout(std430, binding =  1) buffer abc_0 { uint64_t ssFramebuffer[]; };
layout(std430, binding = 44) buffer abc_1 { uint32_t ssRGBA[]; };

layout(std140, binding = 31) uniform UniformData{
	mat4 world;
	mat4 view;
	mat4 proj;
	mat4 transform;
	mat4 transformFrustum;
	int pointsPerThread;
	int enableFrustumCulling;
	int showBoundingBox;
	int numPoints;
	ivec2 imageSize;
	bool colorizeChunks;
	bool colorizeOverdraw;
} uniforms;

layout (std430, binding = 30) buffer abc_2 { 
	uint32_t value;
	bool enabled;
	uint32_t numPointsProcessed;
	uint32_t numNodesProcessed;
	uint32_t numPointsRendered;
	uint32_t numNodesRendered;
	uint32_t numPointsVisible;
} debug;

float getDepthAt(ivec2 pixelCoords, int window){

	float closestDepth = 100000000.0;

	for(int ox = -window; ox <= window; ox++){
	for(int oy = -window; oy <= window; oy++){

		int pixelID = (pixelCoords.x + ox) + (pixelCoords.y + oy) * uniforms.imageSize.x;

		uint64_t data = ssFramebuffer[pixelID];
		uint32_t uDepth = uint32_t(data >> 32l);
		uint32_t pointID = uint32_t(data & 0xffffffffl);
		float depth = uintBitsToFloat(uDepth);

		if(depth > 0.0 && depth < closestDepth){
			closestDepth = depth;
		}
	}
	}

	return closestDepth;
}

void main(){

	uvec2 id = gl_LocalInvocationID.xy;
	id.x += gl_WorkGroupSize.x * gl_WorkGroupID.x;
	id.y += gl_WorkGroupSize.y * gl_WorkGroupID.y;

	ivec2 imgSize = uniforms.imageSize;

	// { // 1 pixel
	// 	ivec2 pixelCoords = ivec2(id);
	// 	ivec2 sourceCoords = ivec2(id);
	// 	int pixelID = sourceCoords.x + sourceCoords.y * uniforms.imageSize.x;

	// 	uint64_t data = ssFramebuffer[pixelID];
	// 	uint32_t pointID = uint32_t(data & 0xFFFFFFFFul);
	// 	bool isLodPoint = ((data & 0x80000000ul) != 0);
	// 	bool isBackground = (data & 0xFFFFFFFFul) > 0xfffffff0u;
		
	// 	uint color;
	// 	if(isLodPoint){
	// 		color = lodColor.data[pointID];
	// 		// color = 0x0000ffFF;
	// 	}else{
	// 		// color = ssRGBA[pointID];
	// 		color = pointID;
	// 	}

	// 	if(isBackground){
	// 		color = 0x000000FF;
	// 	}

	// 	imageAtomicExchange(uOutput, pixelCoords, color);
	// }

	{ // n x n pixel
		ivec2 pixelCoords = ivec2(id);

		float R = 0;
		float G = 0;
		float B = 0;
		float count = 0;

		int window = 0;
		int edlWindow = 1;

		float closestDepth = 1000000.0;
		uint32_t closestPointID = 0;

		for(int ox = -window; ox <= window; ox++){
		for(int oy = -window; oy <= window; oy++){

			int pixelID = (pixelCoords.x + ox) + (pixelCoords.y + oy) * imgSize.x;

			uint64_t data = ssFramebuffer[pixelID];
			uint32_t uDepth = uint32_t(data >> 32l);
			uint32_t pointID = uint32_t(data & 0xffffffffl);
			float depth = uintBitsToFloat(uDepth);

			if(depth > 0.0 && depth < closestDepth){
				closestDepth = depth;
				closestPointID = pointID;
			}
			
		}
		}

		uint32_t color = 0;
		
		if(uniforms.colorizeChunks){
			color = closestPointID;
		}else{
			color = ssRGBA[closestPointID];
		}

		if(closestPointID == 0){
			color = 0x00443322;
		}else{
			if(debug.enabled){
				atomicAdd(debug.numPointsVisible, 1);
			}
		}

		if(false)
		{ // EDL
			int pixelID = (pixelCoords.x) + (pixelCoords.y) * imgSize.x;

			uint64_t data = ssFramebuffer[pixelID];
			uint32_t uDepth = uint32_t(data >> 32l);
			uint32_t pointID = uint32_t(data & 0xffffffffl);
			float depth = uintBitsToFloat(uDepth);

			float sum = 0.0;
			for(int ox = -edlWindow; ox <= edlWindow; ox++){
			for(int oy = -edlWindow; oy <= edlWindow; oy++){

				int pixelID = (pixelCoords.x + ox) + (pixelCoords.y + oy) * imgSize.x;

				// uint64_t data = ssFramebuffer[pixelID];
				// uint32_t uDepth = uint32_t(data >> 32l);
				// uint32_t pointID = uint32_t(data & 0xffffffffl);
				// float neighbourDepth = uintBitsToFloat(uDepth);
				float neighbourDepth = getDepthAt(pixelCoords + ivec2(ox, oy), window);

				sum += max(0.0, depth - neighbourDepth);

				
			}
			}

			float edlStrength = 0.0005;
			float response = sum / 9.0;
			float shade = exp(-response * 300.0 * edlStrength);
			// shade = 1.0;

			uint R = ((color >>  0) & 0xFF);
			uint G = ((color >>  8) & 0xFF);
			uint B = ((color >> 16) & 0xFF);
			
			R = uint(float(R) * shade);
			G = uint(float(G) * shade);
			B = uint(float(B) * shade);

			color = R | (G << 8) | (B << 16);

			// color = uint(255.0 * shade);
		}

		imageAtomicExchange(uOutput, pixelCoords, color);
	}
}