#version 450

// Frustum culling code adapted from three.js
// see https://github.com/mrdoob/three.js/blob/c7d06c02e302ab9c20fe8b33eade4b61c6712654/src/math/Frustum.js
// Three.js license: MIT
// see https://github.com/mrdoob/three.js/blob/a65c32328f7ac4c602079ca51078e3e4fee3123e/LICENSE

#extension GL_NV_shader_atomic_int64 : enable
#extension GL_NV_gpu_shader5 : enable
#extension GL_KHR_shader_subgroup_arithmetic : require
#extension GL_NV_shader_atomic_float : require

#define STEPS_30BIT 1073741824
#define MASK_30BIT 1073741823
#define STEPS_20BIT 1048576
#define MASK_20BIT 1048575
#define STEPS_10BIT 1024
#define MASK_10BIT 1023

#define Infinity (1.0 / 0.0)

struct Batch{
	int state;
	float min_x;
	float min_y;
	float min_z;
	float max_x;
	float max_y;
	float max_z;
	int numPoints;

	int firstPoint;
	int fileIndex;
	int padding3;
	int padding4;
	int padding5;
	int padding6;
	int padding7;
	int padding8;
};

// metadata for each file
// each file has its own worldViewProj matrix
struct File{
	mat4 transform;
	mat4 transformFrustum;
	mat4 world;
	mat4 padding;
};

struct BoundingBox{
	vec4 position;   //   16   0
	vec4 size;       //   16  16
	uint color;      //    4  32
	                 // size: 48
};

struct Point{
	vec3 position;
	uint32_t color;
};

layout(local_size_x = 128, local_size_y = 1) in;

layout (std430, binding =  1) buffer abc_0 { uint64_t ssFramebuffer[]; };
layout (std430, binding = 40) buffer abc_2 { Batch    ssBatches[];     };
layout (std430, binding = 41) buffer abc_3 { uint32_t ssXyz_12b[];     };
layout (std430, binding = 42) buffer abc_4 { uint32_t ssXyz_8b[];      };
layout (std430, binding = 43) buffer abc_5 { uint32_t ssXyz_4b[];      };
layout (std430, binding = 44) buffer abc_6 { uint32_t ssRGBA[];        };
layout (std430, binding = 45) buffer abc_7 { File     ssFiles[];     };

layout (std430, binding = 30) buffer abc_1 { 
	uint32_t value;
	bool enabled;
	uint32_t numPointsProcessed;
	uint32_t numNodesProcessed;
	uint32_t numPointsRendered;
	uint32_t numNodesRendered;
	uint32_t numPointsVisible;
} debug;

layout (std430, binding = 50) buffer data_bb {
	uint count;
	uint instanceCount;
	uint first;
	uint baseInstance;
	uint pad0; uint pad1; uint pad2; uint pad3;
	uint pad4; uint pad5; uint pad6; uint pad7;
	// offset: 48
	BoundingBox ssBoxes[];
} boundingBoxes;

layout(std140, binding = 31) uniform UniformData{
	mat4 pad0; //mat4 world;
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

uint SPECTRAL[5] = {
	0x00ba832b,
	0x00a4ddab,
	0x00bfffff,
	0x0061aefd,
	0x001c19d7
};

struct Plane{
	vec3 normal;
	float constant;
};

float t(int index){

	int a = index % 4;
	int b = index / 4;

	return uniforms.transformFrustum[b][a];
}

float distanceToPoint(vec3 point, Plane plane){
	return dot(plane.normal, point) + plane.constant;
}

Plane createPlane(float x, float y, float z, float w){

	float nLength = length(vec3(x, y, z));

	Plane plane;
	plane.normal = vec3(x, y, z) / nLength;
	plane.constant = w / nLength;

	return plane;
}

Plane[6] frustumPlanes(mat4 worldViewProj){

	float m_0  = worldViewProj[0][0];
	float m_1  = worldViewProj[0][1];
	float m_2  = worldViewProj[0][2];
	float m_3  = worldViewProj[0][3];
	float m_4  = worldViewProj[1][0];
	float m_5  = worldViewProj[1][1];
	float m_6  = worldViewProj[1][2];
	float m_7  = worldViewProj[1][3];
	float m_8  = worldViewProj[2][0];
	float m_9  = worldViewProj[2][1];
	float m_10 = worldViewProj[2][2];
	float m_11 = worldViewProj[2][3];
	float m_12 = worldViewProj[3][0];
	float m_13 = worldViewProj[3][1];
	float m_14 = worldViewProj[3][2];
	float m_15 = worldViewProj[3][3];

	Plane planes[6] = {
		createPlane(m_3 - m_0, m_7 - m_4, m_11 -  m_8, m_15 - m_12),
		createPlane(m_3 + m_0, m_7 + m_4, m_11 +  m_8, m_15 + m_12),
		createPlane(m_3 + m_1, m_7 + m_5, m_11 +  m_9, m_15 + m_13),
		createPlane(m_3 - m_1, m_7 - m_5, m_11 -  m_9, m_15 - m_13),
		createPlane(m_3 - m_2, m_7 - m_6, m_11 - m_10, m_15 - m_14),
		createPlane(m_3 + m_2, m_7 + m_6, m_11 + m_10, m_15 + m_14),
	};

	return planes;
}

bool intersectsFrustum(mat4 worldViewProj, vec3 wgMin, vec3 wgMax){

	Plane[] planes = frustumPlanes(worldViewProj);
	
	for(int i = 0; i < 6; i++){

		Plane plane = planes[i];

		vec3 vector;
		vector.x = plane.normal.x > 0.0 ? wgMax.x : wgMin.x;
		vector.y = plane.normal.y > 0.0 ? wgMax.y : wgMin.y;
		vector.z = plane.normal.z > 0.0 ? wgMax.z : wgMin.z;

		float d = distanceToPoint(vector, plane);

		if(d < 0){
			return false;
		}
	}

	return true;
}

int getPrecisionLevel(vec3 wgMin, vec3 wgMax, mat4 world){
	
	vec3 wgCenter = (wgMin + wgMax) / 2.0;
	float wgRadius = distance(wgMin, wgMax);

	vec4 viewCenter = uniforms.view * world * vec4(wgCenter, 1.0);
	vec4 viewEdge = viewCenter + vec4(wgRadius, 0.0, 0.0, 0.0);

	vec4 projCenter = uniforms.proj * viewCenter;
	vec4 projEdge = uniforms.proj * viewEdge;

	projCenter.xy = projCenter.xy / projCenter.w;
	projEdge.xy = projEdge.xy / projEdge.w;

	vec2 screenCenter = uniforms.imageSize.xy * (projCenter.xy + 1.0) / 2.0;
	vec2 screenEdge = uniforms.imageSize.xy * (projEdge.xy + 1.0) / 2.0;
	float pixelSize = distance(screenEdge, screenCenter);

	int level = 0;
	if(pixelSize < 100){
		level = 4;
	}else if(pixelSize < 200){
		level = 3;
	}else if(pixelSize < 500){
		level = 2;
	}else if(pixelSize < 10000){
		level = 1;
	}else{
		level = 0;
	}

	return level;
}

void main(){
	
	uint batchIndex = gl_WorkGroupID.x;
	Batch batch = ssBatches[batchIndex];
	File file = ssFiles[batch.fileIndex];

	uint wgFirstPoint = batch.firstPoint;

	if(debug.enabled && gl_LocalInvocationID.x == 0){
		atomicAdd(debug.numNodesProcessed, 1);
	}

	vec3 wgMin = vec3(batch.min_x, batch.min_y, batch.min_z);
	vec3 wgMax = vec3(batch.max_x, batch.max_y, batch.max_z);
	vec3 boxSize = wgMax - wgMin;

	// FRUSTUM CULLING
	if((uniforms.enableFrustumCulling != 0) && !intersectsFrustum(file.transformFrustum, wgMin, wgMax)){
		return;
	}

	int level = getPrecisionLevel(wgMin, wgMax, file.world);

	// POPULATE BOUNDING BOX BUFFER, if enabled
	if((uniforms.showBoundingBox != 0) && gl_LocalInvocationID.x == 0){ 
		uint boxIndex = atomicAdd(boundingBoxes.instanceCount, 1);

		boundingBoxes.count = 24;
		boundingBoxes.first = 0;
		boundingBoxes.baseInstance = 0;

		vec3 worldMin = (file.world * vec4(wgMin, 1.0)).xyz;
		vec3 worldMax = (file.world * vec4(wgMax, 1.0)).xyz;

		vec3 wgPos = (worldMin + worldMax) / 2.0;
		vec3 wgSize = worldMax - worldMin;

		uint color = 0x0000FF00;
		if(level > 0){
			color = 0x000000FF;
		}

		color = SPECTRAL[level];

		BoundingBox box;
		box.position = vec4(wgPos, 0.0);
		box.size = vec4(wgSize, 0.0);
		box.color = color;

		boundingBoxes.ssBoxes[boxIndex] = box;
	}

	if(debug.enabled && gl_LocalInvocationID.x == 0){
		atomicAdd(debug.numNodesRendered, 1);
	}

	int loopSize = uniforms.pointsPerThread;

	#define RENDER_STRIDE_128
	// #define RENDER_4_CONSECUTIVE
	
	#if defined(RENDER_STRIDE_128)

	for(int i = 0; i < loopSize; i++){

		uint localIndex = i * gl_WorkGroupSize.x + gl_LocalInvocationID.x;
		uint index = wgFirstPoint + localIndex;

	#elif defined(RENDER_4_CONSECUTIVE)

	uint rollSize = 4;
	uint jump = 0;
	for(int i = 0; i < loopSize; i++){

		if(i > 0 && (i % rollSize) == 0){
			jump++;
		}

		uint localIndex = jump * rollSize * gl_WorkGroupSize.x + rollSize * gl_LocalInvocationID.x + i % rollSize;
		uint index = wgFirstPoint + localIndex;

	#endif

		if(localIndex >= batch.numPoints){
			return;
		}

		if(debug.enabled){
			atomicAdd(debug.numPointsProcessed, 1);
		}

		vec3 point;
		
		// - to reduce memory bandwidth, we load lower precision coordinates for smaller screen-size bounding boxes
		// - coordinates are stored in up to three 4 byte values
		// - A single 4 byte value holds 3 x 10 bit integer components (x, y, z)
		// - if more precision is required, we can load another 4 byte with additional 3 x 10 bit precision
		// - 10 bits grants us 1024 individual values => we can present 1 meter in millimeter precision
		//   20 bits: 1km in mm precsision
		//   30 bits: 1000km in mm precision (but not really because we are loosing precision due to float conversions)
		if(level == 0){
			// 12 byte ( 30 bit per axis)
			uint32_t b4 = ssXyz_4b[index];
			uint32_t b8 = ssXyz_8b[index];
			uint32_t b12 = ssXyz_12b[index];

			uint32_t X_4 = (b4 >>  0) & MASK_10BIT;
			uint32_t Y_4 = (b4 >> 10) & MASK_10BIT;
			uint32_t Z_4 = (b4 >> 20) & MASK_10BIT;

			uint32_t X_8 = (b8 >>  0) & MASK_10BIT;
			uint32_t Y_8 = (b8 >> 10) & MASK_10BIT;
			uint32_t Z_8 = (b8 >> 20) & MASK_10BIT;

			uint32_t X_12 = (b12 >>  0) & MASK_10BIT;
			uint32_t Y_12 = (b12 >> 10) & MASK_10BIT;
			uint32_t Z_12 = (b12 >> 20) & MASK_10BIT;

			uint32_t X = (X_4 << 20) | (X_8 << 10) | X_12;
			uint32_t Y = (Y_4 << 20) | (Y_8 << 10) | X_12;
			uint32_t Z = (Z_4 << 20) | (Z_8 << 10) | X_12;

			float x = float(X) * (boxSize.x / STEPS_30BIT) + wgMin.x;
			float y = float(Y) * (boxSize.y / STEPS_30BIT) + wgMin.y;
			float z = float(Z) * (boxSize.z / STEPS_30BIT) + wgMin.z;

			point = vec3(x, y, z);
		}else if(level == 1){ 
			// 8 byte (20 bits per axis)

			uint32_t b4 = ssXyz_4b[index];
			uint32_t b8 = ssXyz_8b[index];

			uint32_t X_4 = (b4 >>  0) & MASK_10BIT;
			uint32_t Y_4 = (b4 >> 10) & MASK_10BIT;
			uint32_t Z_4 = (b4 >> 20) & MASK_10BIT;

			uint32_t X_8 = (b8 >>  0) & MASK_10BIT;
			uint32_t Y_8 = (b8 >> 10) & MASK_10BIT;
			uint32_t Z_8 = (b8 >> 20) & MASK_10BIT;

			uint32_t X = (X_4 << 20) | (X_8 << 10);
			uint32_t Y = (Y_4 << 20) | (Y_8 << 10);
			uint32_t Z = (Z_4 << 20) | (Z_8 << 10);

			float x = float(X) * (boxSize.x / STEPS_30BIT) + wgMin.x;
			float y = float(Y) * (boxSize.y / STEPS_30BIT) + wgMin.y;
			float z = float(Z) * (boxSize.z / STEPS_30BIT) + wgMin.z;

			point = vec3(x, y, z);
		}else{ 
			// 4 byte (10 bits per axis)

			uint32_t encoded = ssXyz_4b[index];

			uint32_t X = (encoded >>  0) & MASK_10BIT;
			uint32_t Y = (encoded >> 10) & MASK_10BIT;
			uint32_t Z = (encoded >> 20) & MASK_10BIT;

			float x = float(X) * (boxSize.x / STEPS_10BIT) + wgMin.x;
			float y = float(Y) * (boxSize.y / STEPS_10BIT) + wgMin.y;
			float z = float(Z) * (boxSize.z / STEPS_10BIT) + wgMin.z;

			point = vec3(x, y, z);
		}
		
		// now project to screen
		vec4 pos = vec4(point, 1.0);
		pos = file.transform * pos;
		pos.xyz = pos.xyz / pos.w;

		bool isInsideFrustum = !(pos.w <= 0.0 || pos.x < -1.0 || pos.x > 1.0 || pos.y < -1.0 || pos.y > 1.0);

		if(isInsideFrustum){
			vec2 imgPos = (pos.xy * 0.5 + 0.5) * uniforms.imageSize;
			ivec2 pixelCoords = ivec2(imgPos);
			int pixelID = pixelCoords.x + pixelCoords.y * uniforms.imageSize.x;

			
			uint64_t colorComponent;

			if(uniforms.colorizeChunks){
				colorComponent = batchIndex * 1234567;
			}else{
				colorComponent = uint64_t(index);
			}

			uint32_t depth = floatBitsToInt(pos.w);
			uint64_t newPoint = (uint64_t(depth) << 32UL) | colorComponent;

			uint64_t oldPoint = ssFramebuffer[pixelID];
			if(newPoint < oldPoint){
				
				atomicMin(ssFramebuffer[pixelID], newPoint);

				if(debug.enabled){
					atomicAdd(debug.numPointsRendered, 1);
				}

			}
		}

	}

}