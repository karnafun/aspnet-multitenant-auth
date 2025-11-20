import api from './axios';

export const usersApi = {
  getAllUsers: async () => {
    const response = await api.get('/api/users');
    return response.data;
  },
};