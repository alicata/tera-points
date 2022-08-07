
#pragma once

#include <string>
#include <unordered_map>
#include <map>

#include "glm/common.hpp"

#include "data/point_clouds_loader.h"

using namespace std;

struct Runtime{

	inline static vector<Method*> methods;
	inline static Method* selectedMethod = nullptr;
	inline static Resource* resource = nullptr;
	inline static vector<int> keyStates = vector<int>(65536, 0);
	inline static glm::dvec2 mousePosition = {0.0, 0.0};
	inline static int mouseButtons = 0;
	inline static shared_ptr<PointCloudLoader> pointclouds_loader = nullptr;

	Runtime(){
		
	}

	static Runtime* getInstance(){
		static Runtime* instance = new Runtime();

		return instance;
	}

	static void		addMethod(Method* method){

		Runtime::methods.push_back(method);

	}

	static void setSelectedMethod(string name){
		for(Method* method : Runtime::methods){
			if(method->name == name){
				Runtime::selectedMethod = method;
			}
		}
	}

	static Method* getSelectedMethod(){
		return Runtime::selectedMethod;
	}

};