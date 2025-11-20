import api from './axios';

export const adminsApi = {
  getAllUsers: async () => {
    const response = await api.get('/api/admins/users');
    return response.data;
  },
    getUserByEmail: async (email) => {
    const response = await api.get(`/api/admins/users/${email}`);
    return response.data;
  },
};