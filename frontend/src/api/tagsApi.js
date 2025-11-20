import api from './axios';

export const tagsApi = {
  getAllTags: async () => {
    const response = await api.get('/api/tags');
    return response.data;
  },
};