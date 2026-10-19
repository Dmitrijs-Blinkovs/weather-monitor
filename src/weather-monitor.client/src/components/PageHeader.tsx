import type { ReactNode } from 'react';

interface PageHeaderProps {
  title: string;
  description: string;
  aside?: ReactNode;
}

export function PageHeader({ title, description, aside }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div>
        <h1 className="page-header__title">{title}</h1>
        <p className="page-header__description">{description}</p>
      </div>
      {aside && <div className="page-header__aside">{aside}</div>}
    </header>
  );
}
