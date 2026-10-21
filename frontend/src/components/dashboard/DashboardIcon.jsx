const paths = {
  dashboard: 'M3 3h7v7H3z M14 3h7v4h-7z M14 11h7v10h-7z M3 14h7v7H3z',
  equipment: 'M5 4h14v12H5z M9 20h6 M12 16v4 M9 8h6 M9 12h3',
  agents: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M16 4a4 4 0 0 1 0 8 M22 21v-2a4 4 0 0 0-3-3.87 M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
  maintenance: 'M14.7 6.3a5 5 0 0 0-6.4 6.4L3 18a2.1 2.1 0 0 0 3 3l5.3-5.3a5 5 0 0 0 6.4-6.4l-3 3-3-3z',
  company: 'M4 21V5h10v16 M14 9h6v12 M8 9h2 M8 13h2 M8 17h2 M17 13h1 M17 17h1 M2 21h20',
  map: 'm3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2z M9 3v16 M15 5v16',
  heat: 'M12 3c1 5 6 6 6 11a6 6 0 0 1-12 0c0-3 2-5 3-6 0 3 1 4 2 4 2-2 2-5 1-9z',
  alerts: 'm12 3 10 18H2z M12 9v4 M12 17h.01',
  predictive: 'm3 17 6-6 4 4 8-10 M15 5h6v6',
  report: 'M6 3h12v18H6z M9 7h6 M9 11h6 M9 15h2 M9 18h6',
  settings: 'M9 3h6l1 3 3 1 2 5-2 5-3 1-1 3H9l-1-3-3-1-2-5 2-5 3-1z M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
  user: 'M20 21a8 8 0 0 0-16 0 M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
  logout: 'M9 3H4v18h5 M14 8l5 4-5 4 M8 12h11',
  collapse: 'm14 6-6 6 6 6 M20 6l-6 6 6 6',
  menu: 'M4 6h16 M4 12h16 M4 18h16',
  close: 'm6 6 12 12 M6 18 18 6',
  refresh: 'M20 7v5h-5 M4 17v-5h5 M6 6a8 8 0 0 1 13 3 M18 18A8 8 0 0 1 5 15',
  bell: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9 M10 21h4',
  shield: 'm12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z m-4 9 3 3 5-6',
  availability: 'M4 19a9 9 0 1 1 16 0 M12 13l5-5 M11 13a1 1 0 1 0 2 0 1 1 0 0 0-2 0',
  filter: 'M3 5h18l-7 8v6l-4 2v-8z'
};

export default function DashboardIcon({ name, className = '' }) {
  return (
    <svg className={`dashboard-icon ${className}`} viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" focusable="false">
      <path d={paths[name] || paths.dashboard} />
    </svg>
  );
}
