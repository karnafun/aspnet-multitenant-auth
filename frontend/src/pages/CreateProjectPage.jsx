import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { projectsApi } from '../api/projectsApi';
import { tagsApi } from '../api/tagsApi';
import { usersApi } from '../api/usersApi';

function CreateProjectPage() {
    const navigate = useNavigate();

    // Form state
    const [title, setTitle] = useState('');
    const [description, setDescription] = useState('');
    const [status, setStatus] = useState('Open');
    const [availableStatuses, setAvailableStatuses] = useState([]);
    const [assignedToEmail, setAssignedToEmail] = useState('');
    const [selectedTags, setSelectedTags] = useState([]);
    const [selectedWatchers, setSelectedWatchers] = useState([]);

    // Dropdown data
    const [availableTags, setAvailableTags] = useState([]);
    const [availableUsers, setAvailableUsers] = useState([]);

    // UI state
    const [isLoading, setIsLoading] = useState(true);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        loadDropdownData();
    }, []);

    const loadDropdownData = async () => {
        try {
            setIsLoading(true);
            const [tags, users, statuses] = await Promise.all([
                tagsApi.getAllTags(),
                usersApi.getAllUsers(),
                projectsApi.getStatuses(),
            ]);
            setAvailableTags(tags);
            setAvailableUsers(users);
            setAvailableStatuses(statuses);
        } catch (err) {
            setError('Failed to load form data');
        } finally {
            setIsLoading(false);
        }
    };

    const handleTagToggle = (tagSlug) => {
        setSelectedTags(prev =>
            prev.includes(tagSlug)
                ? prev.filter(t => t !== tagSlug)
                : [...prev, tagSlug]
        );
    };

    const handleWatcherToggle = (userEmail) => {
        setSelectedWatchers(prev =>
            prev.includes(userEmail)
                ? prev.filter(e => e !== userEmail)
                : [...prev, userEmail]
        );
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setIsSubmitting(true);

        try {
            const projectData = {
                title,
                description: description || null,
                status,
                assignedTo: assignedToEmail || null,
                tags: selectedTags,
                watchers: selectedWatchers,
            };

            const newProject = await projectsApi.createProject(projectData);
            navigate(`/projects/${newProject.id}`);
        } catch (err) {
            setError(err.response?.data?.detail || 'Failed to create project');
        } finally {
            setIsSubmitting(false);
        }
    };

    if (isLoading) {
        return (
            <div className="min-h-screen flex items-center justify-center">
                <div className="text-center">
                    <div className="text-4xl mb-4">⏳</div>
                    <p className="text-text-secondary">Loading form...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen p-8">
            <div className="max-w-3xl mx-auto">
                <div className="mb-6">
                    <button
                        onClick={() => navigate('/projects')}
                        className="text-text-secondary hover:text-text-primary mb-4"
                    >
                        ← Back to Projects
                    </button>
                    <h1 className="text-3xl font-bold">Create New Project</h1>
                </div>

                <form onSubmit={handleSubmit} className="card space-y-6">
                    {/* Title */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Title <span className="text-error">*</span>
                        </label>
                        <input
                            type="text"
                            value={title}
                            onChange={(e) => setTitle(e.target.value)}
                            className="input"
                            placeholder="Fix login bug"
                            required
                            maxLength={200}
                        />
                    </div>

                    {/* Description */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Description
                        </label>
                        <textarea
                            value={description}
                            onChange={(e) => setDescription(e.target.value)}
                            className="input min-h-[100px]"
                            placeholder="Detailed description..."
                            maxLength={1000}
                        />
                    </div>

                    {/* Status */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Status <span className="text-error">*</span>
                        </label>
                        <select
                            value={status}
                            onChange={(e) => setStatus(Number(e.target.value))} 
                            className="input"
                            required
                        >
                            {availableStatuses.map((s) => (
                                <option key={s.value} value={s.value}>
                                    {s.displayName}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Assigned To */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Assigned To
                        </label>
                        <select
                            value={assignedToEmail}
                            onChange={(e) => setAssignedToEmail(e.target.value)}
                            className="input"
                        >
                            <option value="">-- Unassigned --</option>
                            {availableUsers.map((user) => (
                                <option key={user.email} value={user.email}>
                                    {user.email}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Tags */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Tags
                        </label>
                        <div className="flex flex-wrap gap-2">
                            {availableTags.map((tag) => (
                                <button
                                    key={tag.slug}
                                    type="button"
                                    onClick={() => handleTagToggle(tag.slug)}
                                    className={`px-3 py-1 rounded-lg text-sm transition-colors ${selectedTags.includes(tag.slug)
                                        ? 'bg-accent-green text-primary-bg'
                                        : 'bg-primary-surface border border-primary-border hover:border-accent-green'
                                        }`}
                                >
                                    {tag.name}
                                </button>
                            ))}
                            {availableTags.length === 0 && (
                                <p className="text-text-secondary text-sm">No tags available</p>
                            )}
                        </div>
                    </div>

                    {/* Watchers */}
                    <div>
                        <label className="block text-sm text-text-secondary mb-2">
                            Watchers
                        </label>
                        <div className="space-y-2">
                            {availableUsers.map((user) => (
                                <label
                                    key={user.email}
                                    className="flex items-center gap-2 cursor-pointer hover:text-accent-green transition-colors"
                                >
                                    <input
                                        type="checkbox"
                                        checked={selectedWatchers.includes(user.email)}
                                        onChange={() => handleWatcherToggle(user.email)}
                                        className="w-4 h-4"
                                    />
                                    <span className="text-sm">{user.email}</span>
                                </label>
                            ))}
                        </div>
                    </div>

                    {/* Error Message */}
                    {error && (
                        <div className="text-error text-sm text-center">
                            {error}
                        </div>
                    )}

                    {/* Submit Buttons */}
                    <div className="flex gap-4">
                        <button
                            type="submit"
                            disabled={isSubmitting}
                            className="btn btn-primary flex-1"
                        >
                            {isSubmitting ? 'Creating...' : 'Create Project'}
                        </button>
                        <button
                            type="button"
                            onClick={() => navigate('/projects')}
                            className="btn btn-secondary"
                        >
                            Cancel
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

export default CreateProjectPage;