import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { projectsApi } from '../api/projectsApi';
import { tagsApi } from '../api/tagsApi';
import { usersApi } from '../api/usersApi';

function ProjectDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  // Project data
  const [project, setProject] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Edit mode
  const [isEditMode, setIsEditMode] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  // Edit form state
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editStatus, setEditStatus] = useState('');
  const [editAssignedTo, setEditAssignedTo] = useState('');

  // Dropdown data for editing
  const [availableStatuses, setAvailableStatuses] = useState([]);
  const [availableUsers, setAvailableUsers] = useState([]);
  const [availableTags, setAvailableTags] = useState([]);

  // Tags/Watchers management
  const [isAddingTag, setIsAddingTag] = useState(false);
  const [isAddingWatcher, setIsAddingWatcher] = useState(false);

  useEffect(() => {
    loadProject();
  }, [id]);

  const loadProject = async () => {
    try {
      setIsLoading(true);
      const data = await projectsApi.getProjectById(id);
      setProject(data);
      
      // Initialize edit form with current values
      setEditTitle(data.title);
      setEditDescription(data.description || '');
      setEditStatus(data.status);
      setEditAssignedTo(data.assignedTo?.email || '');
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to load project');
    } finally {
      setIsLoading(false);
    }
  };

  const loadEditData = async () => {
    try {
      const [statuses, users, tags] = await Promise.all([
        projectsApi.getStatuses(),
        usersApi.getAllUsers(),
        tagsApi.getAllTags(),
      ]);
      setAvailableStatuses(statuses);
      setAvailableUsers(users);
      setAvailableTags(tags);
    } catch (err) {
      setError('Failed to load edit data');
    }
  };

  const canEdit = () => {
    if (!user || !project) return false;
    return user.roles?.includes("Admin") || user.email === project.createdBy?.email;
  };

  const handleEdit = async () => {
    await loadEditData();
    setIsEditMode(true);
  };

  const handleCancelEdit = () => {
    // Reset form to original values
    setEditTitle(project.title);
    setEditDescription(project.description || '');
    setEditStatus(project.status);
    setEditAssignedTo(project.assignedTo?.email || '');
    setIsEditMode(false);
  };

  const handleSave = async () => {
    try {
      setIsSaving(true);
      await projectsApi.updateProject(id, {
        title: editTitle,
        description: editDescription || null,
        status: editStatus,
        assignedTo: editAssignedTo || null,
      });
      
      // Reload project to show updated data
      await loadProject();
      setIsEditMode(false);
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to update project');
    } finally {
      setIsSaving(false);
    }
  };

  const handleAddTag = async (tagSlug) => {
    try {
      await projectsApi.addTag(id, tagSlug);
      await loadProject();
      setIsAddingTag(false);
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to add tag');
    }
  };

  const handleRemoveTag = async (tagSlug) => {
    try {
      await projectsApi.removeTag(id, tagSlug);
      await loadProject();
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to remove tag');
    }
  };

  const handleAddWatcher = async (watcherEmail) => {
    try {
      await projectsApi.addWatcher(id, watcherEmail);
      await loadProject();
      setIsAddingWatcher(false);
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to add watcher');
    }
  };

  const handleRemoveWatcher = async (watcherEmail) => {
    try {
      await projectsApi.removeWatcher(id, watcherEmail);
      await loadProject();
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to remove watcher');
    }
  };

  const handleDelete = async () => {
    if (!window.confirm('Are you sure you want to delete this project?')) {
      return;
    }

    try {
      await projectsApi.deleteProject(id);
      navigate('/projects');
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to delete project');
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="text-4xl mb-4">⏳</div>
          <p className="text-text-secondary">Loading project...</p>
        </div>
      </div>
    );
  }

  if (error && !project) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="card max-w-md">
          <p className="text-error mb-4">{error}</p>
          <button onClick={() => navigate('/projects')} className="btn btn-primary">
            Back to Projects
          </button>
        </div>
      </div>
    );
  }

  const availableTagsToAdd = availableTags.filter(
    tag => !project.tags.some(t => t.slug === tag.slug)
  );

  const availableUsersToWatch = availableUsers.filter(
    u => !project.watchers.some(w => w.email === u.email)
  );

  return (
    <div className="min-h-screen p-8">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-6">
          <button
            onClick={() => navigate('/projects')}
            className="text-text-secondary hover:text-text-primary mb-4"
          >
            ← Back to Projects
          </button>
          
          <div className="flex justify-between items-start">
            <div className="flex-1">
              {isEditMode ? (
                <input
                  type="text"
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  className="input text-3xl font-bold mb-2"
                  required
                />
              ) : (
                <h1 className="text-3xl font-bold mb-2">{project.title}</h1>
              )}
              
              <p className="text-text-secondary text-sm">
                Created {new Date(project.createdAt).toLocaleDateString()} by {project.createdBy.email}
              </p>
            </div>

            {canEdit() && (
              <div className="flex gap-2">
                {!isEditMode ? (
                  <>
                    <button onClick={handleEdit} className="btn btn-primary">
                      Edit
                    </button>
                    <button onClick={handleDelete} className="btn btn-secondary">
                      Delete
                    </button>
                  </>
                ) : (
                  <>
                    <button
                      onClick={handleSave}
                      disabled={isSaving}
                      className="btn btn-primary"
                    >
                      {isSaving ? 'Saving...' : 'Save'}
                    </button>
                    <button onClick={handleCancelEdit} className="btn btn-secondary">
                      Cancel
                    </button>
                  </>
                )}
              </div>
            )}
          </div>
        </div>

        {error && (
          <div className="bg-error/10 border border-error text-error px-4 py-3 rounded mb-6">
            {error}
          </div>
        )}

        <div className="space-y-6">
          {/* Description */}
          <div className="card">
            <h2 className="text-xl font-semibold mb-4">Description</h2>
            {isEditMode ? (
              <textarea
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
                className="input min-h-[150px]"
                placeholder="Add a description..."
              />
            ) : (
              <p className="text-text-secondary whitespace-pre-wrap">
                {project.description || 'No description'}
              </p>
            )}
          </div>

          {/* Details */}
          <div className="card">
            <h2 className="text-xl font-semibold mb-4">Details</h2>
            <div className="space-y-3">
              {/* Status */}
              <div className="flex justify-between items-center">
                <span className="text-text-secondary">Status:</span>
                {isEditMode ? (
                  <select
                    value={editStatus}
                    onChange={(e) => setEditStatus(Number(e.target.value))}
                    className="input w-48"
                  >
                    {availableStatuses.map(s => (
                      <option key={s.value} value={s.value}>
                        {s.displayName}
                      </option>
                    ))}
                  </select>
                ) : (
                  <span className="px-3 py-1 rounded bg-accent-green text-primary-bg">
                    {availableStatuses.find(s => s.value === project.status)?.displayName || project.status}
                  </span>
                )}
              </div>

              {/* Assigned To */}
              <div className="flex justify-between items-center">
                <span className="text-text-secondary">Assigned To:</span>
                {isEditMode ? (
                  <select
                    value={editAssignedTo}
                    onChange={(e) => setEditAssignedTo(e.target.value)}
                    className="input w-48"
                  >
                    <option value="">-- Unassigned --</option>
                    {availableUsers.map(u => (
                      <option key={u.email} value={u.email}>
                        {u.email}
                      </option>
                    ))}
                  </select>
                ) : (
                  <span>{project.assignedTo?.email || 'Unassigned'}</span>
                )}
              </div>
            </div>
          </div>

          {/* Tags */}
          <div className="card">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-semibold">Tags</h2>
              {canEdit() && !isAddingTag && (
                <button
                  onClick={() => {
                    if (!availableTags.length) loadEditData();
                    setIsAddingTag(true);
                  }}
                  className="text-accent-green hover:text-accent-green-hover"
                >
                  + Add Tag
                </button>
              )}
            </div>

            {isAddingTag && (
              <div className="mb-4 flex flex-wrap gap-2">
                {availableTagsToAdd.map(tag => (
                  <button
                    key={tag.slug}
                    onClick={() => handleAddTag(tag.slug)}
                    className="px-3 py-1 rounded bg-primary-bg border border-primary-border hover:border-accent-green"
                  >
                    + {tag.name}
                  </button>
                ))}
                <button
                  onClick={() => setIsAddingTag(false)}
                  className="px-3 py-1 rounded bg-primary-bg border border-error text-error"
                >
                  Cancel
                </button>
              </div>
            )}

            <div className="flex flex-wrap gap-2">
              {project.tags.length === 0 ? (
                <p className="text-text-secondary">No tags</p>
              ) : (
                project.tags.map(tag => (
                  <div
                    key={tag.slug}
                    className="flex items-center gap-2 px-3 py-1 rounded bg-accent-green text-primary-bg"
                  >
                    <span>{tag.name}</span>
                    {canEdit() && (
                      <button
                        onClick={() => handleRemoveTag(tag.slug)}
                        className="hover:text-error"
                      >
                        ×
                      </button>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Watchers */}
          <div className="card">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-semibold">Watchers</h2>
              {canEdit() && !isAddingWatcher && (
                <button
                  onClick={() => {
                    if (!availableUsers.length) loadEditData();
                    setIsAddingWatcher(true);
                  }}
                  className="text-accent-green hover:text-accent-green-hover"
                >
                  + Add Watcher
                </button>
              )}
            </div>

            {isAddingWatcher && (
              <div className="mb-4 space-y-2">
                {availableUsersToWatch.map(u => (
                  <button
                    key={u.email}
                    onClick={() => handleAddWatcher(u.email)}
                    className="block w-full text-left px-3 py-2 rounded bg-primary-bg border border-primary-border hover:border-accent-green"
                  >
                    + {u.email}
                  </button>
                ))}
                <button
                  onClick={() => setIsAddingWatcher(false)}
                  className="w-full px-3 py-2 rounded bg-primary-bg border border-error text-error"
                >
                  Cancel
                </button>
              </div>
            )}

            <div className="space-y-2">
              {project.watchers.length === 0 ? (
                <p className="text-text-secondary">No watchers</p>
              ) : (
                project.watchers.map(watcher => (
                  <div
                    key={watcher.email}
                    className="flex justify-between items-center px-3 py-2 rounded bg-primary-bg"
                  >
                    <span>{watcher.email}</span>
                    {canEdit() && (
                      <button
                        onClick={() => handleRemoveWatcher(watcher.email)}
                        className="text-error hover:text-error/80"
                      >
                        Remove
                      </button>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default ProjectDetailPage;