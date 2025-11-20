import api from './axios';

export const projectsApi = {
    getAllProjects: async () => {
        const response = await api.get('/api/projects');
        return response.data;
    },

    getProjectById: async (id) => {
        const response = await api.get(`/api/projects/${id}`);
        return response.data;
    },

    createProject: async (projectData) => {
        const response = await api.post('/api/projects', projectData);
        return response.data;
    },

    updateProject: async (id, projectData) => {
        const response = await api.put(`/api/projects/${id}`, projectData);
        return response.data;
    },

    deleteProject: async (id) => {
        const response = await api.delete(`/api/projects/${id}`);
        return response.data;
    },
    getStatuses: async () => {
        const response = await api.get('/api/projects/statuses');
        return response.data;
    },
    addTag: async (projectId, tagSlug) => {
        const response = await api.post(`/api/projects/${projectId}/tags/${tagSlug}`);
        return response.data;
    },

    removeTag: async (projectId, tagSlug) => {
        const response = await api.delete(`/api/projects/${projectId}/tags/${tagSlug}`);
        return response.data;
    },

    addWatcher: async (projectId, watcherEmail) => {
        const response = await api.post(`/api/projects/${projectId}/watchers/${watcherEmail}`);
        return response.data;
    },

    removeWatcher: async (projectId, watcherEmail) => {
        const response = await api.delete(`/api/projects/${projectId}/watchers/${watcherEmail}`);
        return response.data;
    },

};